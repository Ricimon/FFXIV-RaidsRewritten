// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/PartyListProcessor.cs
// 41fc913
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusPartyListProcessor
{
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    private int[] NumStatuses = [0, 0, 0, 0, 0, 0, 0, 0];
    public StatusPartyListProcessor(DalamudServices dalamudServices, StatusCommonProcessor statusCommonProcessor, EcsContainer ecsContainer, CommonQueries commonQueries, ILogger logger)
    {
        this.dalamudServices = dalamudServices;
        this.statusCommonProcessor = statusCommonProcessor;
        this.ecsContainer = ecsContainer;
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
        //logger.Info($"PartyList Requested update: {NumStatuses.Print()}");
    }

    private record struct UpdatePartyListHelper(AtkResNode*[] IconArray, int CurIndex);
    public void UpdatePartyList(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        if (!StatusCommonProcessor.IsAddonReady(addon)) { return; }

        var partyMemberNodeIndex = 23;
        var party = GetVisibleParty();

        var pPlayerDict = new Dictionary<nint, UpdatePartyListHelper>();
        for (var n = 0; n < party.Count; n++)
        {
            var player = party[n];
            if (player != nint.Zero)
            {
                var iconArray = GetNodeIconArray(addon->UldManager.NodeList[partyMemberNodeIndex]);
                //InternalLog.Information($"Icon array length for {player} is {iconArray.Length}");
                for (var i = NumStatuses[n]; i < iconArray.Length; i++)
                {
                    var c = iconArray[i];
                    if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
                }
                var curIndex = NumStatuses[n];
                pPlayerDict[player] = new UpdatePartyListHelper(iconArray, curIndex);
            }
            partyMemberNodeIndex--;
        }
        if (hideAll) { return; }
        commonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component p) =>
        {
            if (p.PlayerCharacter is null) { return; }
            if (!pPlayerDict.TryGetValue(p.PlayerCharacter.Address, out var player)) { return; }

            var statusQuery = StatusCommonProcessor.GetAllStatusesOfEntity(e);
            statusQuery.Each((ref condition, ref status) =>
            {
                if (player.CurIndex >= player.IconArray.Length) { return; }

                if (condition.TimeRemaining > 0)
                {
                    SetIcon(addon, player.IconArray[player.CurIndex], ref status, ref condition);
                    player.CurIndex++;
                }
            });
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
                
                // Ensure validity.
                if (obj != null && ((Character*)obj.Address)->IsCharacter())
                {
                    ret.Add(obj.Address);
                } else
                {
                    ret.Add(nint.Zero);
                }
            }
            return ret;
        }
    }

    private void SetIcon(AtkUnitBase* addon, AtkResNode* container, ref Condition.Status status, ref Condition.Component component)
    {
        statusCommonProcessor.SetIcon(addon, ref status, ref component, container);
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
}
