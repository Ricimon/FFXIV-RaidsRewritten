// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/PartyListProcessor.cs
// 41fc913
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Memory;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Data;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusPartyListProcessor
{
    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly ResourceLoader resourceLoader;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    private int prevNumStatuses = -1;
    private int ActiveNodeIdForTooltip = -1;
    private AtkResNode* ActiveContainerForTooltip = null;
    private bool dirty = false;

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

    public void Dispose()
    {
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_PartyList", OnPartyListUpdate);
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_PartyList", OnAlcPartyListRequestedUpdate);
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage 
    private void OnAlcPartyListRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void OnPartyListUpdate(AddonEvent type, AddonArgs args)
    {
        UpdatePartyList((AtkUnitBase*)args.Addon.Address);
    }

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;
        if (!StatusCommonProcessor.IsAddonReady(addonBase)) return;

        for (var i = 0; i < NumStatuses.Length; i++)
        {
            NumStatuses[i] = 0;
        }
        var index = 23;
        var storeIndex = 0;
        foreach (nint player in GetVisibleParty())
        {
            //InternalLog.Verbose($"  Now checking {index} for {player}");
            if (player != nint.Zero)
            {
                var iconArray = GetNodeIconArray(addonBase->UldManager.NodeList[index]);
                foreach (var x in iconArray)
                {
                    if (x->IsVisible()) NumStatuses[storeIndex]++;
                }
            }
            storeIndex++;
            index--;
        }
        RefreshTooltip(addonBase);
        //logger.Info($"PartyList Requested update: {NumStatuses.Print()}");
    }

    public unsafe void RefreshTooltip(AtkUnitBase* addon)
    {
        if (ActiveContainerForTooltip != null)
        {
            AtkStage.Instance()->TooltipManager.ShowTooltip(addon->Id, ActiveContainerForTooltip, (byte*)statusCommonProcessor.TooltipMemory);
        }
    }

    public void UpdatePartyList(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!hideAll && (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled)) { return; }
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        if (!StatusCommonProcessor.IsAddonReady(addon)) { return; }

        var partyMemberNodeIndex = 23;
        var party = GetVisibleParty();

        // for each player on the party list, get the array of icon elements on the party list UI
        var pPlayerDict = new Dictionary<nint, AtkResNode*[]>();
        for (var n = 0; n < party.Count; n++)
        {
            var player = party[n];
            if (player != nint.Zero)
            {
                var iconArray = GetNodeIconArray(addon->UldManager.NodeList[partyMemberNodeIndex]);
                //logger.Debug($"Icon array length for {player} is {iconArray.Length}");
                for (var i = NumStatuses[n]; i < iconArray.Length; i++)
                {
                    var c = iconArray[i];
                    if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
                }

                if (hideAll)
                {
                    ResetPartyList(addon, player, iconArray);
                    continue;
                }

                var curIndex = NumStatuses[n];
                pPlayerDict[player] = iconArray;
            }
            partyMemberNodeIndex--;
        }
        if (hideAll) { return; }
        commonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component p) =>
        {
            if (p.PlayerCharacter is null) { return; }
            if (p.PlayerCharacter.EntityId == 0 ) { return; }
            if (!pPlayerDict.TryGetValue(p.PlayerCharacter.Address, out var iconArray)) { return; }

            // avoid processing statuses when custom statuses are absent
            // but ensure that it runs once without custom statuses
            // to clean up
            var statusQuery = StatusCommonProcessor.GetAllStatusesOfEntity(e);
            if (statusQuery.Count() == 0)
            {
                if (!dirty)
                {
                    return;
                } else
                {
                    dirty = false;
                }
            }

            // compile a list of statuses and sort them
            var pChara = p.PlayerCharacter.Character();
            List<Status> statusList = [];
            foreach (var status in pChara->GetStatusManager()->Status)
            {
                var temp = new Status(status);
                if (!temp.IsEnhancement && !temp.IsEnfeeblement && !temp.IsConditionalEnhancement) { continue; }
                if (status.SourceObject == pChara->GetGameObjectId()) { temp.SourceIsSelf = true; }
                statusList.Add(temp);
            }

            statusQuery.Each((e, ref condition, ref status) =>
            {
                if (condition.TimeRemaining > 0)
                {
                    if (e.TryGet<FileReplacement>(out var replacement))
                    {
                        statusList.Add(new Status(status, condition, StatusType.SelfEnfeeblement, replacement));
                    } else
                    {
                        statusList.Add(new Status(status, condition, StatusType.SelfEnfeeblement));
                    }
                    dirty = true;
                }
            });

            var sortedList = statusList
                .OrderBy(s => s.SourceIsSelf)
                .ThenByDescending(s => s.PartyListPriority);

            // force tooltips to be redrawn whenever there's a shift in statuses
            var shouldRedrawTooltip = prevNumStatuses > -1 && sortedList.Count() != prevNumStatuses;
            prevNumStatuses = sortedList.Count();

            int curIndex = 0;
            var hasConfig = this.dalamudServices.GameConfig.UiConfig.TryGet("PartyListStatus", out uint optionInt);
            var maxLength = hasConfig ? Math.Min(iconArray.Length, optionInt - 1) : iconArray.Length;
            foreach (var status in sortedList)
            {
                if (curIndex >= maxLength) { return; }
                SetIcon(addon, iconArray[curIndex], status, shouldRedrawTooltip);
                curIndex++;
            }
        });
    }

    /// <summary>
    ///     Returns a list of pointer addresses that are Character* references for the visible party members.
    /// </summary>
    /// <returns></returns>
    public List<nint> GetVisibleParty()
    {
        if (this.dalamudServices.PartyList.Length < 2)
        {
            return [StatusCommonProcessor.LocalPlayer()];
        } else
        {
            List<nint> ret = [StatusCommonProcessor.LocalPlayer()];
            for (var i = 1; i < Math.Min(8, Svc.Party.Length); i++)
            {
                var obj = this.dalamudServices.PartyList[i]; 
                if (obj != null)
                {
                    if (obj.EntityId != 0)
                    {
                        IGameObject? gameObj = null;
                        dalamudServices.Framework.RunOnFrameworkThread(() =>
                        {
                            gameObj = dalamudServices.ObjectTable.SearchByEntityId(obj.EntityId);
                        });
                        if (gameObj != null)
                        {
                            ret.Add(gameObj.Address);
                        }
                        break;
                    }
                }
                ret.Add(nint.Zero);
            }
            return ret;
        }
    }

    private void SetIcon(AtkUnitBase* addon, AtkResNode* container, Status status, bool redrawTooltip)
    {
        if (!container->IsVisible())
        {
            container->NodeFlags ^= NodeFlags.Visible;
        }

        if (status.IsCustom)
        {
            container->GetAsAtkComponentNode()->Component->GetImageNodeById(3)->LoadTexture(status.OriginalPath);

            // mark node image as "dirty"
            var temp = (Interop.Structs.AtkComponentIconText*)container->GetAsAtkComponentNode()->Component;
            temp->IconId = 0;
        } else
        {
            resourceLoader.LoadIconByID(container->GetAsAtkComponentNode()->Component, (int)status.IconId);
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
        if (statusCommonProcessor.HoveringOver == addr && (ActiveNodeIdForTooltip == -1 || redrawTooltip))
        {
            commonQueries.StatusQuery.Each((ref _, ref status) =>
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

    public static AtkResNode*[] GetNodeIconArray(AtkResNode* node, bool reverse = false)
    {
        var lst = new List<nint>();
        var atk = node->GetAsAtkComponentNode();
        if (atk is null) return [];
        var uldm = atk->Component->UldManager;
        for (var i = 0; i < uldm.NodeListCount; i++)
        {
            var next = uldm.NodeList[i];
            if (next == null) continue;
            if ((int)next->Type < 1000) continue;
            if (((AtkUldComponentInfo*)next->GetAsAtkComponentNode()->Component->UldManager.Objects)->ComponentType == ComponentType.IconText)
            {
                lst.Add((nint)next);
            }
        }
        var ret = new AtkResNode*[lst.Count];
        for (var i = 0; i < lst.Count; i++)
        {
            ret[i] = (AtkResNode*)lst[reverse ? lst.Count - 1 - i : i];
        }
        return ret;
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

            var maxLength = hasConfig ? Math.Min(iconArray.Length, optionInt - 1) : iconArray.Length;
            foreach (var status in sortedList)
            {
                if (currIndex >= maxLength) { return; }
                SetIcon(addon, iconArray[currIndex], status, true);
                currIndex++;
            }
        }
    }
}
