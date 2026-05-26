// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/PartyListProcessor.cs
// 41fc913
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Data;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZLinq;

namespace RaidsRewritten.Memory;

public unsafe class StatusPartyListProcessor
{
    private record struct PlayerDictElement(AtkResNode*[] IconArray, bool Dirty, int PrevNumStatuses, int Order);
    private record struct VisiblePartyElement(nint GameObj, int Order);

    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly ResourceLoader resourceLoader;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    private int ActiveNodeIdForTooltip = -1;
    private AtkResNode* ActiveContainerForTooltip = null;
    private Dictionary<nint, PlayerDictElement> pPlayerDict = [];
    private bool iconArrayRequestUpdate = true;
    private int prevPartyListSize = -1;

    private List<nint> GetNodeIconArrayList = [];

    private int[] NumStatuses = [0, 0, 0, 0, 0, 0, 0, 0];
    public StatusPartyListProcessor(
        Configuration configuration,
        DalamudServices dalamudServices,
        StatusCommonProcessor statusCommonProcessor,
        EcsContainer ecsContainer,
        ResourceLoader resourceLoader,
        CommonQueries commonQueries,
        ILogger logger)
    {
        this.configuration = configuration;
        this.dalamudServices = dalamudServices;
        this.statusCommonProcessor = statusCommonProcessor;
        this.ecsContainer = ecsContainer;
        this.resourceLoader = resourceLoader;
        this.commonQueries = commonQueries;
        this.logger = logger;

        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_PartyList", OnPartyListUpdate);
        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnAlcPartyListRequestedUpdate);

        if (StatusCommonProcessor.LocalPlayerAvailable())
        {
            var addon = statusCommonProcessor.GetAddon("_PartyList");
            if (StatusCommonProcessor.IsAddonReady(addon))
            {
                AddonRequestedUpdate(addon);
            }
        }
    }

    public void HideAll()
    {
        var addon = statusCommonProcessor.GetAddon("_PartyList");
        if (StatusCommonProcessor.IsAddonReady(addon))
        {
            UpdatePartyList(addon, true);
        }
    }

    public void Reset()
    {
        pPlayerDict.Clear();
        iconArrayRequestUpdate = true;
        prevPartyListSize = -1;
        ActiveNodeIdForTooltip = -1;
        ActiveContainerForTooltip = null;
    }

    public void Dispose()
    {
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_PartyList", OnPartyListUpdate);
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnAlcPartyListRequestedUpdate);
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage 
    private void OnAlcPartyListRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void OnPartyListUpdate(AddonEvent type, AddonArgs args) => UpdatePartyList((AtkUnitBase*)args.Addon.Address);

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;
        if (!StatusCommonProcessor.IsAddonReady(addonBase)) return;

        for (var i = 0; i < NumStatuses.Length; i++)
        {
            NumStatuses[i] = 0;
        }

        var storeIndex = 0;
        foreach (var partyElement in GetVisibleParty())
        {
            //InternalLog.Verbose($"  Now checking {index} for {player}");
            if (partyElement.GameObj != nint.Zero)
            {
                if (this.pPlayerDict.TryGetValue(partyElement.GameObj, out var playerElement))
                {
                    // if party list was reordered, rebuild pPlayerDict
                    if (partyElement.Order != playerElement.Order)
                    {
                        iconArrayRequestUpdate = true;
                    }
                    foreach (var x in playerElement.IconArray)
                    {
                        if (x->IsVisible()) NumStatuses[storeIndex]++;
                    }
                } else
                {
                    iconArrayRequestUpdate = true;
                }
            }
            storeIndex++;
        }
        RefreshTooltip(addonBase);
        //logger.Info($"PartyList Requested update: {NumStatuses.Print()}");
    }

    public void UpdatePartyList(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!hideAll && (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled)) { return; }
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        if (!StatusCommonProcessor.IsAddonReady(addon)) { return; }

        HashSet<nint> validPC = [.. CharacterManager.Instance()->BattleCharas.AsValueEnumerable()
            .Where(bc => (BattleChara*)bc != null)
            .Select(bc => (nint)(BattleChara*)bc)];
        // This method can sometimes be called off the main thread, causing the following line to crash
        //HashSet<nint> validPC = [.. this.dalamudServices.ObjectTable.PlayerObjects.Select(pc => pc.Address)];

        var party = GetVisibleParty();
        // some players could be dced, so pPlayerDict.Count is not suitable
        if (party.Count != prevPartyListSize) { iconArrayRequestUpdate = true; }

        //for each player on the party list, get the array of icon elements on the party list UI
        if (iconArrayRequestUpdate)
        {
            iconArrayRequestUpdate = false;
            BuildIconArrayDict();
        }

        // if dirty, hide all visible status icons
        // this is to handle statuses falling off
        // visibility will be reenabled in SetIcon()
        for (var n = 0; n < party.Count; n++)
        {
            var player = party[n].GameObj;
            if (player != nint.Zero)
            {
                if (!this.pPlayerDict.TryGetValue(player, out var element))
                {
                    iconArrayRequestUpdate = true;
                    continue;
                }

                for (var i = NumStatuses[n]; i < element.IconArray!.Length; i++)
                {
                    var c = element.IconArray[i];
                    if (c->IsVisible() && element.Dirty) { c->NodeFlags ^= NodeFlags.Visible; }
                }

                if (hideAll)
                {
                    if (!validPC.Contains(player)) { continue; }
                    ResetPartyList(addon, player, element.IconArray);
                    continue;
                }
            }
        }

        if (hideAll) { return; }

        var redrawTooltip = false;
        commonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component p) =>
        {
            var playerChara = p.PlayerCharacter;
            if (playerChara is null) { return; }
            if (!validPC.Contains(playerChara.Address)) { return; }
            if (!pPlayerDict.TryGetValue(playerChara.Address, out var element))
            {
                // this assumes that any active Player.Component
                // is a valid party member. if there are players referenced by Player.Component
                // that aren't a party member, it will continuously rebuild pPlayerDict
                //logger.Debug($"{p.PlayerCharacter.Name}");
                iconArrayRequestUpdate = true;
                return;
            }

            var pChara = playerChara.Character();

            var hasCustomStatuses = false;
            List<Status> statusList = [];
            e.Children((Entity child) =>
            {
                if (!StatusCommonProcessor.IsCustomStatus(child, out var condition, out var customStatus, out var statusTooltip))
                {
                    return;
                }

                hasCustomStatuses = true;

                // compile a list of statuses and sort them, do it only once
                if (statusList.Count == 0)
                {
                    foreach (var nativeStatus in pChara->GetStatusManager()->Status)
                    {
                        if (nativeStatus.StatusId == 0) { continue; }
                        var temp = new Status(nativeStatus);
                        if (!temp.IsEnhancement && !temp.IsEnfeeblement && !temp.IsConditionalEnhancement) { continue; }
                        if (nativeStatus.SourceObject == pChara->GetGameObjectId()) { temp.SourceIsSelf = true; }
                        statusList.Add(temp);
                    }
                }

                if (condition.TimeRemaining > 0)
                {
                    if (child.TryGet<FileReplacementReference>(out var replacement))
                    {
                        statusList.Add(new Status(customStatus, statusTooltip, condition, StatusType.SelfEnfeeblement, replacement.Replacement));
                    }
                    else
                    {
                        statusList.Add(new Status(customStatus, statusTooltip, condition, StatusType.SelfEnfeeblement));
                    }
                    element.Dirty = true;
                    pPlayerDict[(nint)pChara] = element;
                }

            });

            // avoid processing statuses when custom statuses are absent
            // but ensure that it runs once without custom statuses
            // to clean up
            if (!hasCustomStatuses)
            {
                if (element.Dirty)
                {
                    element.Dirty = false;
                    pPlayerDict[playerChara.Address] = element;
                    ResetPartyList(addon, playerChara.Address, element.IconArray);
                }
                return;
            }

            var sortedList = statusList
                .OrderBy(s => s.SourceIsSelf)
                .ThenByDescending(s => s.PartyListPriority);

            // force tooltips to be redrawn whenever there's a shift in statuses
            var shouldRedrawTooltip = element.PrevNumStatuses > -1 && sortedList.Count() != element.PrevNumStatuses;
            element.PrevNumStatuses = sortedList.Count();
            pPlayerDict[(nint)pChara] = element;
            redrawTooltip |= shouldRedrawTooltip;

            // SetIcon will handle redrawing tooltip
            if (shouldRedrawTooltip)
            {
                ActiveContainerForTooltip = null;
                ActiveNodeIdForTooltip = -1;
            }

            int curIndex = 0;
            var hasConfig = this.dalamudServices.GameConfig.UiConfig.TryGet("PartyListStatus", out uint optionInt);
            var maxLength = hasConfig ? Math.Min(element.IconArray.Length, optionInt) : element.IconArray.Length;
            foreach (var status in sortedList)
            {
                if (curIndex >= maxLength) { return; }
                SetIcon(addon, element.IconArray[curIndex], status);
                curIndex++;
            }

            if (redrawTooltip) { ResetTooltip(addon); }
        });
    }

    private void BuildIconArrayDict()
    {
        this.pPlayerDict.Clear();

        var addon = (AddonPartyList*)dalamudServices.GameGui.GetAddonByName("_PartyList").Address;
        if (addon == null) { return; }

        var party = GetVisibleParty();
        prevPartyListSize = party.Count;
        for (int pMemberIndex = 0; pMemberIndex < party.Count; pMemberIndex++)
        {
            var element = party[pMemberIndex];
            var player = element.GameObj;
            if (player != nint.Zero)
            {
                var icons = addon->PartyMembers[pMemberIndex].StatusIcons;
                AtkResNode*[] iconArray = new AtkResNode*[icons.Length];
                for (int iconIndex = 0; iconIndex < icons.Length; iconIndex++)
                {
                    iconArray[iconIndex] = (AtkResNode*)icons[iconIndex].Value->OwnerNode;
                    //logger.Debug($"{pMemberIndex}: {iconIndex} {(IntPtr)icons[iconIndex].Value->AtkResNode:X2}");
                }
                this.pPlayerDict[player] = new(iconArray, false, -1, element.Order);
            }
        }
    }

    public unsafe void RefreshTooltip(AtkUnitBase* addon)
    {
        if (ActiveContainerForTooltip != null)
        {
            var addr = (nint)ActiveContainerForTooltip->GetAsAtkComponentNode()->Component;
            if (statusCommonProcessor.HoveringOver == addr)
            {
                AtkStage.Instance()->TooltipManager.ShowTooltip(addon->Id, ActiveContainerForTooltip, (byte*)statusCommonProcessor.TooltipMemory);
            } else
            {
                ActiveContainerForTooltip = null;
                ActiveNodeIdForTooltip = -1;
            }
        }
    }

    /// <summary>
    ///     Returns a list of pointer addresses that are Character* references for the visible party members.
    /// </summary>
    /// <returns></returns>
    private List<VisiblePartyElement> GetVisibleParty()
    {
        List<VisiblePartyElement> ret = [new(StatusCommonProcessor.LocalPlayer(), 0)];
        if (this.dalamudServices.PartyList.Length < 2)
        {
            return ret;
        } else
        {
            var pListIndex = 1;
            for (var i = 0; i < Math.Min(8, this.dalamudServices.PartyList.Length); i++)
            {
                var obj = Resolve($"<{pListIndex}>");
                if (obj != null)
                {
                    if ((nint)obj != StatusCommonProcessor.LocalPlayer())
                    {
                        ret.Add(new((nint)obj, pListIndex));
                    }
                } else
                {
                    ret.Add(new(nint.Zero, pListIndex));
                }
                pListIndex++;
            }
            return ret;
        }
    }

    // adapted from https://github.com/NightmareXIV/ECommons/blob/master/ECommons/GameFunctions/ExtendedPronoun.cs
    // e72386f
    // Resolves placeholders from strings like <1> like in macros 
    private GameObject* Resolve(string str) => Framework.Instance()->GetUIModule()->GetPronounModule()->ResolvePlaceholder($"{str}", 0, 0);

    private void SetIcon(AtkUnitBase* addon, AtkResNode* container, Status status)
    {
        if (!container->IsVisible())
        {
            container->NodeFlags ^= NodeFlags.Visible;
        }

        if (!string.IsNullOrEmpty(status.OriginalPath))
        {
            var imageNode = container->GetAsAtkComponentNode()->Component->GetImageNodeById(3);
            imageNode->LoadTexture(status.OriginalPath);

            if (!imageNode->IsVisible())
            {
                imageNode->NodeFlags ^= NodeFlags.Visible;
            }

            // mark node image as "dirty"
            var temp = (Interop.Structs.AtkComponentIconText*)container->GetAsAtkComponentNode()->Component;
            temp->IconId = 0;
        } else
        {
            resourceLoader.LoadIconByID?.Invoke(container->GetAsAtkComponentNode()->Component, (int)status.IconId);
        }

        //var dispelNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[0];

        // timer
        var textNode = container->GetAsAtkComponentNode()->Component->UldManager.NodeList[2];
        var timerText = "";
        if (!float.IsInfinity(status.TimeRemaining))
        {
            timerText = status.TimeRemaining > 0 ? StatusCommonProcessor.GetTimerText(status.TimeRemaining) : "";
        }

        if (timerText != null)
        {
            if (!textNode->IsVisible()) { textNode->NodeFlags ^= NodeFlags.Visible; }
        }

        var t = textNode->GetAsAtkTextNode();
        t->SetText((timerText ?? SeString.Empty).Encode());

        if (status.SourceIsSelf)
        {
            t->TextColor = StatusCommonProcessor.CreateColor(0xC9ffe4ff);
            t->EdgeColor = StatusCommonProcessor.CreateColor(0x0a5f24ff);
            t->BackgroundColor = StatusCommonProcessor.CreateColor(0);
        } else
        {
            t->TextColor = StatusCommonProcessor.CreateColor(0xffffffff);
            t->EdgeColor = StatusCommonProcessor.CreateColor(0x333333ff);
            t->BackgroundColor = StatusCommonProcessor.CreateColor(0);
        }

        // tooltip
        var addr = (nint)container->GetAsAtkComponentNode()->Component;
        if (statusCommonProcessor.HoveringOver == addr && (ActiveNodeIdForTooltip == -1))
        {
            commonQueries.StatusQuery.Each((ref _, ref status, ref _) =>
            {
                status.TooltipShown = -1;
            });
            ActiveNodeIdForTooltip = (int)container->NodeId;

            var str = status.Name;
            if (status.Description != "")
            {
                str += $"\n{status.Description}";
            }
            str += "\0";
            MemoryHelper.WriteSeString(statusCommonProcessor.TooltipMemory, str);
            AtkStage.Instance()->TooltipManager.ShowTooltip(addon->Id, container, (byte*)statusCommonProcessor.TooltipMemory);
            ActiveContainerForTooltip = container;
        }

        if (ActiveNodeIdForTooltip == container->NodeId && statusCommonProcessor.HoveringOver != addr)
        {
            ActiveNodeIdForTooltip = -1;
            ActiveContainerForTooltip = null;
            if (statusCommonProcessor.HoveringOver == 0)
            {
                AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            }
        }
    }

    private void ResetPartyList(AtkUnitBase* addon, nint player, AtkResNode*[] iconArray)
    {
        var gameObj = (GameObject*)player;
        if (gameObj->IsCharacter())
        {
            var pChara = gameObj->GetAsCharacter();
            List<Status> statusList = [];
            foreach (var status in pChara->GetStatusManager()->Status)
            {
                if (status.StatusId == 0) { continue; }
                var temp = new Status(status);
                if (!temp.IsEnhancement && !temp.IsEnfeeblement && !temp.IsConditionalEnhancement) { continue; }
                if (status.SourceObject == pChara->GetGameObjectId()) { temp.SourceIsSelf = true; }
                statusList.Add(temp);
            }
            var sortedList = statusList
                .OrderBy(s => s.SourceIsSelf)
                .ThenByDescending(s => s.PartyListPriority);

            int currIndex = 0;
            var hasConfig = this.dalamudServices.GameConfig.UiConfig.TryGet("PartyListStatus", out uint optionInt);

            // SetIcon will handle redrawing tooltip
            ActiveContainerForTooltip = null;
            ActiveNodeIdForTooltip = -1;

            var maxLength = hasConfig ? Math.Min(iconArray.Length, optionInt) : iconArray.Length;
            foreach (var status in sortedList)
            {
                if (currIndex >= maxLength) { return; }
                SetIcon(addon, iconArray[currIndex], status);
                currIndex++;
            }

            // edge case with 1 custom status 0 native statuses, custom status falling off while tooltip is showing
            if (ActiveContainerForTooltip == null && statusCommonProcessor.HoveringOver != 0)
            {
                AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
            }
        }
    }

    private void ResetTooltip(AtkUnitBase* addon)
    {
        ActiveContainerForTooltip = null;
        ActiveNodeIdForTooltip = -1;
        AtkStage.Instance()->TooltipManager.HideTooltip(addon->Id);
    }
}
