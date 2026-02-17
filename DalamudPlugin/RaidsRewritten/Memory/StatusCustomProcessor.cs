// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/StatusCustomProcessor.cs
// 37e76d3
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RaidsRewritten.Memory;

public unsafe class StatusCustomProcessor : IDisposable
{
    private readonly Configuration configuration;
    private readonly DalamudServices dalamudServices;
    private readonly EcsContainer ecsContainer;
    private readonly ResourceLoader resourceLoader;
    private readonly CommonQueries commonQueries;
    private readonly StatusCommonProcessor statusCommonProcessor;
    private readonly ILogger logger;

    public int NumStatuses0 = 0;
    public int NumStatuses1 = 0;
    public int NumStatuses2 = 0;

    public nint HoveringOver = 0;
    private readonly nint TooltipMemory;

    public StatusCustomProcessor(
        Configuration configuration,
        DalamudServices dalamudServices,
        EcsContainer ecsContainer,
        ResourceLoader resourceLoader,
        CommonQueries commonQueries,
        StatusCommonProcessor statusCommonProcessor,
        ILogger logger)
    {
        this.configuration = configuration;
        this.dalamudServices = dalamudServices;
        this.ecsContainer = ecsContainer;
        this.resourceLoader = resourceLoader;
        this.commonQueries = commonQueries;
        this.statusCommonProcessor = statusCommonProcessor;
        this.logger = logger;

        TooltipMemory = Marshal.AllocHGlobal(2 * 1024);

        dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_StatusCustom0", OnStatusCustom0Update);
        dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom0", OnStatusCustom0RequestedUpdate);
        dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_StatusCustom1", OnStatusCustom1Update);
        dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom1", OnStatusCustom1RequestedUpdate);
        dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "_StatusCustom2", OnStatusCustom2Update);
        dalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom2", OnStatusCustom2RequestedUpdate);

        if (StatusCommonProcessor.LocalPlayerAvailable())
        {
            // enhancements
            var addon0 = this.statusCommonProcessor.GetAddon("_StatusCustom0");
            if (StatusCommonProcessor.IsAddonReady(addon0))
            {
                Custom0RequestedUpdate(addon0);
            }

            // enfeeblements
            var addon1 = this.statusCommonProcessor.GetAddon("_StatusCustom1");
            if (StatusCommonProcessor.IsAddonReady(addon1))
            {
                Custom1RequestedUpdate(addon1);
            }

            // others
            var addon2 = this.statusCommonProcessor.GetAddon("_StatusCustom2");
            if (StatusCommonProcessor.IsAddonReady(addon2))
            {
                Custom2RequestedUpdate(addon2);
            }
        }
    }
    public void Dispose()
    {
        dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_StatusCustom0", OnStatusCustom0Update);
        dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_StatusCustom1", OnStatusCustom1Update);
        dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "_StatusCustom2", OnStatusCustom2Update);
        dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom0", OnStatusCustom0RequestedUpdate);
        dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom1", OnStatusCustom1RequestedUpdate);
        dalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_StatusCustom2", OnStatusCustom2RequestedUpdate);
        Marshal.FreeHGlobal(TooltipMemory);
    }
    public void HideAll()
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;

        var addon0 = statusCommonProcessor.GetAddon("_StatusCustom0");
        if (StatusCommonProcessor.IsAddonReady(addon0))
        {
            AddonStatusCustomPrerequisite(addon0, NumStatuses0);
        }

        var addon1 = statusCommonProcessor.GetAddon("_StatusCustom1");
        if (StatusCommonProcessor.IsAddonReady(addon1))
        {
            AddonStatusCustomPrerequisite(addon1, NumStatuses1);
        }

        var addon2 = statusCommonProcessor.GetAddon("_StatusCustom2");
        if (StatusCommonProcessor.IsAddonReady(addon2))
        {
            AddonStatusCustomPrerequisite(addon2, NumStatuses2);
        }
    }

    private void OnStatusCustom0RequestedUpdate(AddonEvent t, AddonArgs args) => Custom0RequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnStatusCustom1RequestedUpdate(AddonEvent t, AddonArgs args) => Custom1RequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnStatusCustom2RequestedUpdate(AddonEvent t, AddonArgs args) => Custom2RequestedUpdate((AtkUnitBase*)args.Addon.Address);

    private void Custom0RequestedUpdate(AtkUnitBase* addonBase)
    {
        AddonRequestedUpdate(addonBase, ref NumStatuses0);
        //logger.Info($"StatusCustom0 Requested update: {NumStatuses0}");
    }

    private void Custom1RequestedUpdate(AtkUnitBase* addonBase)
    {
        AddonRequestedUpdate(addonBase, ref NumStatuses1);
        //logger.Info($"StatusCustom1 Requested update: {NumStatuses1}");
    }

    private void Custom2RequestedUpdate(AtkUnitBase* addonBase)
    {
        AddonRequestedUpdate(addonBase, ref NumStatuses2);
        //logger.Info($"StatusCustom2 Requested update: {NumStatuses2}");
    }

    // others
    private void OnStatusCustom2Update(AddonEvent type, AddonArgs args)
    {
        if (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled) { return; }
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        //PluginLog.Verbose($"Post1 update {args.Addon:X16}");
        var addon = (AtkUnitBase*)args.Addon.Address;
        int baseCnt = AddonStatusCustomPrerequisite(addon, NumStatuses2);
        commonQueries.StatusOtherQuery.Each((Entity e, ref Condition.Component condition, ref Condition.Status status) =>
        {
            if (baseCnt < 5) return;
            if (e.TryGet<FileReplacement>(out var replacement))
            {
                UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, ref condition, ref status, baseCnt, replacement);
            } else
            {
                UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, ref condition, ref status, baseCnt);

            }
            baseCnt--;
        });
    }

    // enfeeblements
    private void OnStatusCustom1Update(AddonEvent type, AddonArgs args)
    {
        if (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled) { return; }
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        //PluginLog.Verbose($"Post1 update {args.Addon:X16}");
        var addon = (AtkUnitBase*)args.Addon.Address;
        int baseCnt = AddonStatusCustomPrerequisite(addon, NumStatuses1);
        commonQueries.StatusEnfeeblementQuery.Each((Entity e, ref Condition.Component condition, ref Condition.Status status) =>
        {
            if (baseCnt < 5) return;
            if (e.TryGet<FileReplacement>(out var replacement))
            {
                UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, ref condition, ref status, baseCnt, replacement);
            } else
            {
                UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, ref condition, ref status, baseCnt);

            }
            baseCnt--;
        });
    }

    // enhancements
    private void OnStatusCustom0Update(AddonEvent type, AddonArgs args)
    {
        if (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled) { return; }
        if (!StatusCommonProcessor.LocalPlayerAvailable()) { return; }
        //PluginLog.Verbose($"Post1 update {args.Addon:X16}");
        var addon = (AtkUnitBase*)args.Addon.Address;
        int baseCnt = AddonStatusCustomPrerequisite(addon, NumStatuses0);
        commonQueries.StatusEnhancementQuery.Each((Entity e, ref Condition.Component condition, ref Condition.Status status) =>
        {
            if (baseCnt < 5) return;
            if (e.TryGet<FileReplacement>(out var replacement))
            {
                UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, ref condition, ref status, baseCnt, replacement);
            } else
            {
                UpdateStatusCustom((AtkUnitBase*)args.Addon.Address, ref condition, ref status, baseCnt);

            }
            baseCnt--;
        });
    }

    private int AddonStatusCustomPrerequisite(AtkUnitBase* addon, int numStatuses)
    {
        int baseCnt = 24 - numStatuses;
        if (StatusCommonProcessor.IsAddonReady(addon))
        {
            for (var i = baseCnt; i >= 5; i--)
            {
                var c = addon->UldManager.NodeList[i];
                if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
            }
        }
        return baseCnt;
    }

    // The common logic method with all statuses of a defined type in the player's status manager.
    public void UpdateStatusCustom(AtkUnitBase* addon, ref Condition.Component condition, ref Condition.Status status, int baseCnt, FileReplacement? replacement = null)
    {
        SetIcon(addon, baseCnt, ref status, ref condition, replacement);
    }

    private void AddonRequestedUpdate(AtkUnitBase* addon, ref int StatusCnt)
    {
        if (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled) { return; }
        if (!StatusCommonProcessor.IsAddonReady(addon)) { return; }
        StatusCnt = 0;
        for (var i = 24; i >= 5; i--)
        {
            var c = addon->UldManager.NodeList[i];
            if (c->IsVisible())
            {
                StatusCnt++;

                // mark node as dirty
                var temp = (Interop.Structs.AtkComponentIconText*)c->GetAsAtkComponentNode()->Component;
                var iconId = temp->IconId;
                temp->IconId = 0;
                resourceLoader.LoadIconByID(c->GetAsAtkComponentNode()->Component, (int)iconId);
            }
        }
    }

    private unsafe void SetIcon(AtkUnitBase* addon, int index, ref Condition.Status status, ref Condition.Component condition, FileReplacement? replacement = null)
    {
        var container = addon->UldManager.NodeList[index];
        statusCommonProcessor.SetIcon(addon, ref status, ref condition, container, replacement);
    }

}