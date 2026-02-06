// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/StatusProcessor.cs
// 37e76d3
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusProcessor
{
    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    public int NumStatuses = 0;

    public StatusProcessor(Configuration configuration, DalamudServices dalamudServices, StatusCommonProcessor statusCommonProcessor, EcsContainer ecsContainer, CommonQueries commonQueries, ILogger logger)
    {
        this.configuration = configuration;
        this.dalamudServices = dalamudServices;
        this.statusCommonProcessor = statusCommonProcessor;
        this.ecsContainer = ecsContainer;
        this.commonQueries = commonQueries;
        this.logger = logger;

        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_Status", OnStatusUpdate);
        this.dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_Status", OnAlcStatusRequestedUpdate);
        var addon = statusCommonProcessor.GetAddon("_Status");
        if (StatusCommonProcessor.LocalPlayerAvailable() && StatusCommonProcessor.IsAddonReady(addon))
        {
            AddonRequestedUpdate(addon);
        }
    }

    public void Dispose()
    {
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_Status", OnStatusUpdate);
        this.dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_Status", OnAlcStatusRequestedUpdate);
    }

    public void HideAll()
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;


        var addon = statusCommonProcessor.GetAddon("_Status");
        if (StatusCommonProcessor.IsAddonReady(addon))
        {
            UpdateStatus(addon, NumStatuses, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage 
    private void OnAlcStatusRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnStatusUpdate(AddonEvent type, AddonArgs args)
    {
        if (configuration.UseLegacyStatusRendering) { return; }
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;

        UpdateStatus((AtkUnitBase*)args.Addon.Address, NumStatuses);
    }

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!StatusCommonProcessor.IsAddonReady(addonBase)) { return; }
        NumStatuses = 0;
        for (var i = 25; i >= 1; i--)
        {
            var c = addonBase->UldManager.NodeList[i];
            if (c->IsVisible())
            {
                NumStatuses++;
            }
        }
    }

    public void UpdateStatus(AtkUnitBase* addon, int StatusCnt, bool hideAll = false)
    {
        if (addon != null && StatusCommonProcessor.IsAddonReady(addon))
        {
            int baseCnt = 25 - StatusCnt;
            for (var i = baseCnt; i >= 1; i--)
            {
                var c = addon->UldManager.NodeList[i];
                if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
            }

            if (hideAll) { return; }

            commonQueries.LocalPlayerQuery.Each((e, ref player) =>
            {
                var statusQuery = StatusCommonProcessor.GetAllStatusesOfEntity(e);
                statusQuery.Each((ref condition, ref status) =>
                {
                    if (condition.TimeRemaining > 0)
                    {
                        SetIcon(addon, baseCnt, ref status, ref condition);
                        baseCnt--;
                    }
                });
            });
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, ref Condition.Status status, ref Condition.Component condition)
    {
        var container = addon->UldManager.NodeList[index];
        statusCommonProcessor.SetIcon(addon, ref status, ref condition, container);
    }

}
