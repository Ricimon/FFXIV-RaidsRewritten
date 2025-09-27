// Adapted from https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/UiAdjustment/FadeUnavailableActions.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RaidsRewritten.Data;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Interop;

public unsafe sealed class HotbarManager : IDisposable
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

    private delegate void UpdateHotBarSlotDelegate(AddonActionBarBase* addon, ActionBarSlot* uiData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex);
    [Signature("E8 ?? ?? ?? ?? 48 81 C6 ?? ?? ?? ?? 83 C7 11", DetourName = nameof(OnHotBarSlotUpdate))]
    private Hook<UpdateHotBarSlotDelegate> onHotBarSlotUpdateHook = null!;

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private readonly List<string> addonActionBarNames = [
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
        "_ActionCross",
        "_ActionDoubleCrossR",
        "_ActionDoubleCrossL",
    ];

    private bool disableAllActions;
    private bool disableDamagingActions;

    public HotbarManager(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        dalamud.GameInteropProvider.InitializeFromAttributes(this);
    }

    public void Dispose()
    {
        this.disableAllActions = this.disableDamagingActions = false;
        ProcessAllHotBars();
        this.onHotBarSlotUpdateHook.Dispose();
    }

    private void ProcessAllHotBars()
    {
        foreach (var addonName in addonActionBarNames)
        {
            var addon = GetUnitBase<AddonActionBarBase>(addonName);
            if (addon is null) { continue; } 

            //this.logger.Debug($"Addon {addonName}, slots address: 0x{(nint)addon->ActionBarSlotVector.First:X}");
            foreach (var slot in addon->ActionBarSlotVector)
            {
                ProcessHotBarSlot(addon, &slot);
                // slot.ActionId seems to hold a wrong value for slots that don't have a combat action,
                // so this value is not reliable
                // Ex. Limit Break has ActionId 3 (sprint)
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

    private void OnHotBarSlotUpdate(AddonActionBarBase* addon, ActionBarSlot* hotBarSlotData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex)
    {
        //this.logger.Debug($"OnHotBarSlotUpdate addon:{addon->NameString}, numberArrayDataPtr:0x{(nint)numberArray:X} numberArrayIndex:{numberArrayIndex}, stringArrayIndex:{stringArrayIndex}");
        try
        {
            ProcessHotBarSlot(addon, hotBarSlotData);
        }
        catch(Exception e)
        {
            this.logger.Error(e.ToStringFull());
        }

        onHotBarSlotUpdateHook.Original(addon, hotBarSlotData, numberArray, stringArray, numberArrayIndex, stringArrayIndex);
    }

    private void ProcessHotBarSlot(AddonActionBarBase* addon, ActionBarSlot* hotBarSlotData)
    {
        if (!DisableAllActions && !DisableDamagingActions)
        {
            ApplyDarkening(hotBarSlotData, false);
            return;
        }

        // ActionBarSlotVector is a Vector, so its individual components do not have unique addresses,
        // meaning you cannot use addresses in a Find predicate.
        // Instead, we can use a pointer in the ActionBarSlot struct
        var slotIndex = addon->ActionBarSlotVector.FindIndex(s => s.Icon == hotBarSlotData->Icon);
        //this.logger.Debug($"slotIndex:{slotIndex}");
        if (slotIndex < 0) { return; }
        var raptureSlot = Framework.Instance()->GetUIModule()->GetRaptureHotbarModule()->GetSlotById(addon->RaptureHotbarId, (uint)slotIndex);

        var isBlockableAction = 
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Action ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.Item ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.EventItem ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.CraftAction ||
            raptureSlot->ApparentSlotType == RaptureHotbarModule.HotbarSlotType.GeneralAction;

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

    private void ApplyDarkening(ActionBarSlot* hotBarSlotData, bool darken)
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

    // Taken from https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Utility/Common.cs#L80
    private T* GetUnitBase<T>(string? name = null, int index = 1) where T : unmanaged
    {
        if (string.IsNullOrEmpty(name))
        {
            var attr = (AddonAttribute)typeof(T).GetCustomAttribute(typeof(AddonAttribute));
            if (attr != null)
            {
                name = attr.AddonIdentifiers.AsValueEnumerable().FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) { return null; }

        return (T*)this.dalamud.GameGui.GetAddonByName(name, index).Address;
    }
}
