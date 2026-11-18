// Adapted from https://github.com/MidoriKami/VanillaPlus/blob/master/VanillaPlus/Features/FadeUnavailableActions/FadeUnavailableActions.cs
// 05ad69a
using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RaidsRewritten.Data;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Interop;

public sealed class HotbarManager : IDisposable
{
    public bool DisableAllActions
    {
        get => this.disableAllActions;
        set
        {
            if (value != this.disableAllActions)
            {
                this.disableAllActions = value;
                ProcessAllHotBars();
            }
        }
    }

    public bool DisableDamagingActions
    {
        get => this.disableDamagingActions;
        set
        {
            if (value != this.disableDamagingActions)
            {
                this.disableDamagingActions = value;
                ProcessAllHotBars();
            }
        }
    }

    private const byte CrossHotbar1RaptureId = 10;

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private Hook<AddonActionBarBase.Delegates.UpdateHotbarSlot> onHotBarSlotUpdateHook;
    private bool disableAllActions;
    private bool disableDamagingActions;

    public HotbarManager(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        unsafe
        {
            onHotBarSlotUpdateHook = dalamud.GameInteropProvider.HookFromAddress<AddonActionBarBase.Delegates.UpdateHotbarSlot>(AddonActionBarBase.MemberFunctionPointers.UpdateHotbarSlot, OnHotBarSlotUpdate);
        }
    }

    public void Dispose()
    {
        this.disableAllActions = this.disableDamagingActions = false;
        ProcessAllHotBars();
        this.onHotBarSlotUpdateHook.Dispose();
    }

    private unsafe void OnHotBarSlotUpdate(AddonActionBarBase* addon, ActionBarSlot* hotBarSlotData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex)
    {
        //this.logger.Debug($"OnHotBarSlotUpdate addon:{addon->NameString}, numberArrayDataPtr:0x{(nint)numberArray:X} numberArrayIndex:{numberArrayIndex}, stringArrayIndex:{stringArrayIndex}");
        try
        {
            ProcessHotBarSlot(addon, hotBarSlotData);
        }
        catch (Exception e)
        {
            this.logger.Error(e.ToStringFull());
        }
        finally
        {
            onHotBarSlotUpdateHook.Original(addon, hotBarSlotData, numberArray, stringArray, numberArrayIndex, stringArrayIndex);
        }
    }

    private unsafe void ProcessAllHotBars()
    {
        foreach(var addon in RaptureAtkUnitManager.Instance()->AllLoadedUnitsList.Entries)
        {
            if (addon.Value is null) continue;
            if (addon.Value->NameString.Contains("_Action") && !addon.Value->NameString.Contains("Contents"))
            {
                var actionBar = (AddonActionBarBase*)addon.Value;
                if (actionBar is null) continue;
                if (actionBar->ActionBarSlotVector.First is null) continue;

                foreach(var slot in actionBar->ActionBarSlotVector)
                {
                    ProcessHotBarSlot(actionBar, &slot);
                    // slot.ActionId seems to hold a wrong value for slots that don't have a combat action,
                    // so this value is not reliable
                    // Ex. Limit Break has ActionId 3 (sprint)
                }
            }
        }

        if (DisableAllActions || DisableDamagingActions)
        {
            this.onHotBarSlotUpdateHook.Enable();
        }
        else
        {
            this.onHotBarSlotUpdateHook.Disable();
        }
    }

    private unsafe void ProcessHotBarSlot(AddonActionBarBase* addon, ActionBarSlot* hotBarSlotData)
    {
        ApplyDarkening(hotBarSlotData, false);

        if (!DisableAllActions && !DisableDamagingActions)
        {
            return;
        }

        // ActionBarSlotVector is a Vector, so its individual components do not have unique addresses,
        // meaning you cannot use addresses in a Find predicate.
        // Instead, we can use a pointer in the ActionBarSlot struct
        var slotIndex = addon->ActionBarSlotVector.FindIndex(s => s.Icon == hotBarSlotData->Icon);
        if (slotIndex < 0) { return; }

        var raptureHotbarId = addon->RaptureHotbarId;
        if (addon->IsCrossHotbar)
        {
            //logger.Info("Processing crosshotbar id {0}, slotIndex {1}", raptureHotbarId, slotIndex);
            var crossHotbarAddon = (AddonActionCross*)addon;
            if (crossHotbarAddon != null)
            {
                // This value goes:
                // Cross Hotbar 1 - Left = 1
                // Cross Hotbar 1 - Right = 2
                // Cross Hotbar 2 - Left = 3
                if (crossHotbarAddon->ExpandedHoldMapValue != 0)
                {
                    // When using the expanded hotbar accessed through LT+RT, it's necessary to map the current slot value
                    // back to the original rapture hotbar, as the expanded hotbar reuses the 4-11 slot indices,
                    // and does not update its referenced RaptureHotbarId.
                    if (slotIndex < 4 || slotIndex >= 12)
                    {
                        return;
                    }

                    raptureHotbarId = (byte)((crossHotbarAddon->ExpandedHoldMapValue - 1) / 2 + CrossHotbar1RaptureId);
                    var left = crossHotbarAddon->ExpandedHoldMapValue % 2 != 0;
                    if (left)
                    {
                        slotIndex -= 4;
                    }
                    else
                    {
                        slotIndex += 4;
                    }

                    // TODO: This does not work for the WXHB hotbars
                }
            }
        }

        var raptureSlot = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->GetSlotById(raptureHotbarId, (uint)slotIndex);

        //if (addon->IsCrossHotbar)
        //{
        //    logger.Info("Processing crosshotbar id {0}, slotIndex {1}, slotType {2}, actionId {3}", raptureHotbarId, slotIndex, raptureSlot->ApparentSlotType, raptureSlot->ApparentActionId);
        //}
        var isBlockableAction =
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Item ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.EventItem ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.CraftAction ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.GeneralAction;

        // Aether Compass
        if (raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.McGuffin &&
            raptureSlot->ApparentActionId == 4)
        {
            isBlockableAction = true;
        }

        if (!isBlockableAction) { return; }

        if (DisableAllActions)
        {
            ApplyDarkening(hotBarSlotData, true);
            return;
        }

        if (DisableDamagingActions)
        {
            var isDamageAction = Actions.DamageActions.Contains(raptureSlot->ApparentActionId);
            ApplyDarkening(hotBarSlotData, isDamageAction);
            return;
        }
    }

    private unsafe void ApplyDarkening(ActionBarSlot* hotBarSlotData, bool darken)
    {
        if (hotBarSlotData is null) { return; }
        var iconComponent = (AtkComponentIcon*)hotBarSlotData->Icon->Component;

        if (iconComponent is null) { return; }
        if (iconComponent->IconImage is null) { return; }

        if (!darken)
        {
            iconComponent->IconImage->Color.R = 0xFF;
            iconComponent->IconImage->Color.G = 0xFF;
            iconComponent->IconImage->Color.B = 0xFF;
            return;
        }

        var iconIsAlreadyDarkened =
            iconComponent->IconImage->MultiplyRed < 0x64 &&
            iconComponent->IconImage->MultiplyGreen < 0x64 &&
            iconComponent->IconImage->MultiplyBlue < 0x64;

        if (!iconIsAlreadyDarkened)
        {
            iconComponent->IconImage->Color.R = 0x80;
            iconComponent->IconImage->Color.G = 0x80;
            iconComponent->IconImage->Color.B = 0x80;
        }
        else
        {
            iconComponent->IconImage->Color.R = 0xFF;
            iconComponent->IconImage->Color.G = 0xFF;
            iconComponent->IconImage->Color.B = 0xFF;
        }
    }

    private enum NumberArrayActionType : uint
    {
        Empty = 0x0,
        Macro = 0x2F,
        Action = 0x30,
        InventoryItem = 0x32,
        KeyItem = 0x34,
        CraftAction = 0x38,
        MainCommand = 0x3B,
        CollectionItem = 0x4B,
    }
}
