// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/TargetInfoProcessor.cs
// 37e76d3
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusTargetInfoProcessor
{
    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly ResourceLoader resourceLoader;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    public int NumStatuses = 0;
    public StatusTargetInfoProcessor(Configuration configuration,
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

        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_TargetInfo", OnTargetInfoUpdate);
        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfo", OnTargetInfoRequestedUpdate);
        var addon = statusCommonProcessor.GetAddon("_TargetInfo");
        if (StatusCommonProcessor.LocalPlayerAvailable() && StatusCommonProcessor.IsAddonReady(addon))
        {
            AddonRequestedUpdate(addon);
        }
    }

    public void Dispose()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_TargetInfo", OnTargetInfoUpdate);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_TargetInfo", OnTargetInfoRequestedUpdate);
    }

    public void HideAll()
    {
        var addon = statusCommonProcessor.GetAddon("_TargetInfo");
        if (StatusCommonProcessor.IsAddonReady(addon))
        {
            UpdateAddon(addon, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage
    private void OnTargetInfoRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (StatusCommonProcessor.IsAddonReady(addonBase))
        {
            NumStatuses = 0;
            for (var i = 32; i >= 3; i--)
            {
                var c = addonBase->UldManager.NodeList[i];
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
        }
        //InternalLog.Verbose($"TargetInfo Requested update: {NumStatuses}");
    }

    private void OnTargetInfoUpdate(AddonEvent type, AddonArgs args)
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        UpdateAddon((AtkUnitBase*)args.Addon.Address);
    }

    public void UpdateAddon(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!hideAll && (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled)) { return; }
        var target = this.dalamudServices.TargetManager.SoftTarget! ?? this.dalamudServices.TargetManager.Target!;
        if (target is IPlayerCharacter pc)
        {
            if (!StatusCommonProcessor.IsAddonReady(addon)) { return; }
            int baseCnt = 32 - NumStatuses;
            for (var i = baseCnt; i >= 3; i--)
            {
                var c = addon->UldManager.NodeList[i];
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
                        if (baseCnt < 3) { return; }
                        if (condition.TimeRemaining > 0)
                        {
                            if (e.TryGet<FileReplacement>(out var replacement))
                            {
                                SetIcon(addon, baseCnt, ref status, ref condition, replacement);
                            } else
                            {
                                SetIcon(addon, baseCnt, ref status, ref condition);
                            }
                            baseCnt--;
                        }
                    });
                }
            });
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, ref Condition.Status status, ref Condition.Component condition, FileReplacement? replacement = null)
    {
        var container = addon->UldManager.NodeList[index];
        statusCommonProcessor.SetIcon(addon, ref status, ref condition, container, replacement);
    }
}
