// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/StatusProcessor.cs
// 37e76d3
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
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

public unsafe class StatusProcessor
{
    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly EcsContainer ecsContainer;
    private readonly ResourceLoader resourceLoader;
    private readonly CommonQueries commonQueries;
    private readonly ILogger logger;

    public int NumStatuses = 0;

    public StatusProcessor(
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
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;

        UpdateStatus((AtkUnitBase*)args.Addon.Address, NumStatuses);
    }

    private int GetAddonStatusElementFlag()
    {
        var uiModule = UIModule.Instance();
        if (uiModule == null) { return -1; }
        var addonConfig = uiModule->GetAddonConfig();
        if (addonConfig == null) { return -1; }
        var activeDataSet = addonConfig->ActiveDataSet;
        if (activeDataSet == null) { return -1; }
        return (int)activeDataSet->HudLayoutConfigEntries[3].ElementFlags;
    }

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!StatusCommonProcessor.IsAddonReady(addonBase)) { return; }
        NumStatuses = 0;

        // nodelist # right (low #) to left (high #)
        var startIndex = 30;
        if (GetAddonStatusElementFlag() == 1)
        {
            // if "normal" status bar is selected, put custom statuses on right side
            // in this case, we're only appending to debuff area for now.
            // order is nodes 6 through 1 (still populates left to right despite reversed node #s)
            startIndex = 6;
        } 
        for (var i = startIndex; i >= 1; i--)
        {
            var c = addonBase->UldManager.NodeList[i];
            if (c->IsVisible())
            {
                NumStatuses++;

                // mark node as dirty
                var temp = (Interop.Structs.AtkComponentIconText*)c->GetAsAtkComponentNode()->Component;
                var iconId = temp->IconId;
                temp->IconId = 0;
                resourceLoader.LoadIconByID?.Invoke(c->GetAsAtkComponentNode()->Component, (int)iconId);
            }
        }
    }

    public void UpdateStatus(AtkUnitBase* addon, int StatusCnt, bool hideAll = false)
    {
        if (!hideAll && (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled)) { return; }
        if (addon != null && StatusCommonProcessor.IsAddonReady(addon))
        {
            // nodelist is right (low #) to left (high #)
            var startIndex = 30;
            if (GetAddonStatusElementFlag() == 1)
            {
                // if "normal" status bar is selected, put custom statuses on right side
                // order is nodes 6 through 1 (still populates left to right despite reversed node #s)
                startIndex = 6;
            }

            // start populating custom statuses to the right (subtract) of the leftmost node
            int baseCnt = startIndex - NumStatuses;  
            for (var i = baseCnt; i >= 1; i--)
            {
                var c = addon->UldManager.NodeList[i];
                if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
            }

            if (hideAll) { return; }

            commonQueries.LocalPlayerQuery.Each((e, ref player) =>
            {
                var statusQuery = StatusCommonProcessor.GetAllStatusesOfEntity(e);
                statusQuery.Each((e, ref condition, ref status) =>
                {
                    // rightmost node reached
                    if (baseCnt <= 0) { return; }
                    if (condition.TimeRemaining > 0)
                    {
                        if (e.TryGet<FileReplacementReference>(out var replacement))
                        {
                            SetIcon(addon, baseCnt, ref status, ref condition, replacement.Replacement);
                        } else
                        {
                            SetIcon(addon, baseCnt, ref status, ref condition);
                        }
                        // traverse left to right
                        baseCnt--;
                    }
                });
            });
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, ref Condition.Status status, ref Condition.Component condition, FileReplacement? replacement = null)
    {
        var container = addon->UldManager.NodeList[index];
        statusCommonProcessor.SetIcon(addon, ref status, ref condition, container, replacement);
    }

}
