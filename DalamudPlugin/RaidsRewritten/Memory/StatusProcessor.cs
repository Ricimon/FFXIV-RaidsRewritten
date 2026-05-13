// adapted from https://github.com/kawaii/Moodles/blob/main/Moodles/GameGuiProcessors/StatusProcessor.cs
// 37e76d3
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RaidsRewritten.Game;
using RaidsRewritten.Interop;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

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

    private enum DisplayOption
    {
        Normal,
        LeftJustified1,
        LeftJustified2,
        LeftJustified3,
    }

    private int rightmostRealStatusIndex;

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
            UpdateStatus(addon, true);
        }
    }

    // Func helper to get around 7.4's internal AddonArgs while removing ArtificialAddonArgs usage 
    private void OnAlcStatusRequestedUpdate(AddonEvent t, AddonArgs args) => AddonRequestedUpdate((AtkUnitBase*)args.Addon.Address);
    private void OnStatusUpdate(AddonEvent type, AddonArgs args)
    {
        if (!StatusCommonProcessor.LocalPlayerAvailable()) return;

        UpdateStatus((AtkUnitBase*)args.Addon.Address);
    }

    private DisplayOption GetDisplayOption()
    {
        var addon = statusCommonProcessor.GetAddon("_Status");
        return addon->Param switch
        {
            1 => DisplayOption.Normal,
            17 => DisplayOption.LeftJustified1,
            33 => DisplayOption.LeftJustified2,
            49 => DisplayOption.LeftJustified3,
            _ => DisplayOption.LeftJustified1,
        };
    }

    private void AddonRequestedUpdate(AtkUnitBase* addonBase)
    {
        if (!StatusCommonProcessor.IsAddonReady(addonBase)) { return; }

        var startIndex = 30;
        if (GetDisplayOption() == DisplayOption.Normal)
        {
            // "Normal" display puts the first debuff on index 6
            startIndex = 6;
        }
        // LeftJustified2 orders statuses as Enhancements Space Enfeeblements
        // Without any real Enfeeblements, fake statuses will go right next to Enhancements, not properly leaving a space.
        // This is too annoying to solve so TODO, I guess - Ricimon
        rightmostRealStatusIndex = startIndex + 1;

        for (var i = 1; i <= startIndex; i++)
        {
            var c = addonBase->UldManager.NodeList[i];
            if (c->IsVisible())
            {
                rightmostRealStatusIndex = i;
                break;
            }
        }

        // nodelist # right (low #) to left (high #)
        for (var i = startIndex; i >= 1; i--)
        {
            var c = addonBase->UldManager.NodeList[i];
            if (c->IsVisible())
            {
                // mark node as dirty to place real statuses back
                var temp = (Interop.Structs.AtkComponentIconText*)c->GetAsAtkComponentNode()->Component;
                var iconId = temp->IconId;
                temp->IconId = 0;
                resourceLoader.LoadIconByID?.Invoke(c->GetAsAtkComponentNode()->Component, (int)iconId);
            }
        }
    }

    public void UpdateStatus(AtkUnitBase* addon, bool hideAll = false)
    {
        if (!hideAll && (configuration.UseLegacyStatusRendering || configuration.EverythingDisabled)) { return; }
        if (addon != null && StatusCommonProcessor.IsAddonReady(addon))
        {
            // nodelist is right (low #) to left (high #)
            // start populating custom statuses to the right (subtract) of the rightmost node
            // first, hide all nodes without real statuses
            int baseCnt = rightmostRealStatusIndex - 1;
            for (var i = baseCnt; i >= 1; i--)
            {
                var c = addon->UldManager.NodeList[i];
                if (c->IsVisible()) c->NodeFlags ^= NodeFlags.Visible;
            }

            if (hideAll) { return; }

            commonQueries.LocalPlayerQuery.Each((e, ref player) =>
            {
                var statusQuery = StatusCommonProcessor.GetAllStatusesOfEntity(e);
                statusQuery.Each((e, ref condition, ref status, ref statusTooltip) =>
                {
                    // rightmost node reached
                    if (baseCnt <= 0) { return; }
                    if (condition.TimeRemaining > 0)
                    {
                        if (e.TryGet<FileReplacementReference>(out var replacement))
                        {
                            SetIcon(addon, baseCnt, ref status, ref statusTooltip, ref condition, replacement.Replacement);
                        } else
                        {
                            SetIcon(addon, baseCnt, ref status, ref statusTooltip, ref condition);
                        }
                        // traverse left to right
                        baseCnt--;
                    }
                });
            });
        }
    }

    private void SetIcon(AtkUnitBase* addon, int index, ref Condition.Status status, ref Condition.StatusTooltip statusTooltip, ref Condition.Component condition, FileReplacement? replacement = null)
    {
        var container = addon->UldManager.NodeList[index];
        statusCommonProcessor.SetIcon(addon, ref status, ref statusTooltip, ref condition, container, replacement);
    }

}
