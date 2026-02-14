// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/TargetInfoBuffDebuffProcessor.cs
// 37e76d3
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusTargetInfoBuffDebuffProcessor
{
    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly ResourceLoader resourceLoader;
    private readonly EcsContainer ecsContainer;
    private readonly CommonQueries commonQueries;

    public int NumStatuses = 0;
    public StatusTargetInfoBuffDebuffProcessor(
        Configuration configuration,
        DalamudServices dalamudServices,
        StatusCommonProcessor statusCommonProcessor,
        EcsContainer ecsContainer,
        ResourceLoader resourceLoader,
        CommonQueries commonQueries)
    {
        this.configuration = configuration;
        this.dalamudServices = dalamudServices;
        this.statusCommonProcessor = statusCommonProcessor;
        this.ecsContainer = ecsContainer;
        this.resourceLoader = resourceLoader;
        this.commonQueries = commonQueries;

        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffUpdate);
        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffRequestedUpdate);

        var addon = statusCommonProcessor.GetAddon("_TargetInfoBuffDebuff");
        if (StatusCommonProcessor.LocalPlayerAvailable() && StatusCommonProcessor.IsAddonReady(addon))
        {
            AddonRequestedUpdate(addon);
        }
    }

    public void Dispose()
    {
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffUpdate);
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfoBuffDebuff", OnTargetInfoBuffDebuffRequestedUpdate);
    }

    public void HideAll()
    {
        var addon = statusCommonProcessor.GetAddon("_TargetInfoBuffDebuff");
        if (StatusCommonProcessor.IsAddonReady(addon))
        {
            UpdateAddon(addon, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private void OnTargetInfoBuffDebuffRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!StatusCommonProcessor.IsAddonReady(addonBase)) { return; }
        NumStatuses = 0;
        for (var i = 3u; i <= 32; i++)
        {
            var c = addonBase->UldManager.SearchNodeById(i);
            if (c->IsVisible())
            {
                NumStatuses++;

                // mark node as dirty
                var temp = (Interop.Structs.AtkComponentIconText*)c->GetAsAtkComponentNode()->Component;
                var iconId = temp->IconId;
                temp->IconId = 0;
                resourceLoader.LoadIconByID(c->GetAsAtkComponentNode()->Component, (int)iconId);
            }
        }
        //InternalLog.Verbose($"TargetInfo Requested update: {NumStatuses}");
    }

    private void OnTargetInfoBuffDebuffUpdate(AddonEvent type, AddonArgs args)
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;
        UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    // Didn't really know how to transfer to get the DalamudStatusList from here, so had to use IPlayerCharacter.
    public unsafe void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!hideAll && (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled)) { return; }
        var target = this.dalamudServices.TargetManager.SoftTarget! ?? this.dalamudServices.TargetManager.Target!;
        if (target is IPlayerCharacter pc)
        {
            if (StatusCommonProcessor.IsAddonReady(addon))
            {
                int baseCnt = 3 + NumStatuses;
                for (var i = baseCnt; i <= 32; i++)
                {
                    var c = addon->UldManager.SearchNodeById((uint)i);
                    if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
                }
                
                if (hideAll) { return; }

                commonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component player) =>
                {
                    if (player.PlayerCharacter != null && player.PlayerCharacter.Address == target.Address)
                    {
                        var statusQuery = StatusCommonProcessor.GetAllStatusesOfEntity(e);
                        statusQuery.Each((e, ref condition, ref status) =>
                        {
                            if (condition.TimeRemaining > 0)
                            {
                                if (e.TryGet<FileReplacement>(out var replacement))
                                {
                                    SetIcon(addon, baseCnt, ref status, ref condition, replacement);
                                } else
                                {
                                    SetIcon(addon, baseCnt, ref status, ref condition, replacement);
                                }
                                baseCnt++;
                            }
                        });
                    }
                });
            }
        }
    }

    private void SetIcon(AtkUnitBase* addon, int id, ref Condition.Status status, ref Condition.Component condition, FileReplacement? replacement = null)
    {
        var container = addon->UldManager.SearchNodeById((uint)id);
        statusCommonProcessor.SetIcon(addon, ref status, ref condition, container, replacement);
    }


}
