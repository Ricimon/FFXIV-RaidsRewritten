// Adapted from https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Tweaks/UiAdjustment/FadeUnavailableActions.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;

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
                if (value)
                {
                    this.onHotBarSlotUpdateHook.Enable();
                }
                else
                {
                    this.onHotBarSlotUpdateHook.Disable();
                }
                ProcessAllHotBars();
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x40)]
    private struct NumberArrayStruct {
        [FieldOffset(0x00)] public NumberArrayActionType ActionType;
        [FieldOffset(0x0C)] public uint ActionId;
        [FieldOffset(0x14)] public bool ActionAvailable_1;
        [FieldOffset(0x18)] public bool ActionAvailable_2;
        [FieldOffset(0x20)] public int CooldownPercent;
        [FieldOffset(0x28)] public int ManaCost;
        [FieldOffset(0x40)] public bool TargetInRange;
    }

    private enum NumberArrayActionType: uint {
        Action = 0x2E,
        CraftAction = 0x36,
        
        // Note, 7.0 added new a new type before 0x2E, and potentially more after, unused values were removed.
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

    private readonly Dictionary<uint, Lumina.Excel.Sheets.Action?> actionCache = [];

    private bool disableAllActions;

    public HotbarManager(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        dalamud.GameInteropProvider.InitializeFromAttributes(this);
    }

    public void Dispose()
    {
        this.onHotBarSlotUpdateHook.Dispose();
    }

    private void ProcessAllHotBars()
    {
        foreach (var addonName in addonActionBarNames)
        {
            var addon = GetUnitBase<AddonActionBarBase>(addonName);
            if (addon is null) { continue; }

            this.logger.Debug($"Addon {addonName}");
            foreach (var slot in addon->ActionBarSlotVector)
            {
                if (!DisableAllActions)
                {
                    // Reset everything if turning off
                    ApplyDarkening(&slot, false);
                }
                //else
                //{
                //    // Be picky if turning on
                //    var action = GetAction((uint)slot.ActionId);
                //    if (action != null)
                //    {
                //        this.logger.Debug($"Hotbar {slot.HotbarId} action {action.Value.Name}({slot.ActionId}) actionCategory:{action.Value.ActionCategory.Value.Name} behaviorType:{action.Value.BehaviourType}, isPlayerAction:{action.Value.IsPlayerAction}");
                //        ApplyDarkening(&slot, true);
                //    }
                //}
            }
        }
    }

    private void OnHotBarSlotUpdate(AddonActionBarBase* addon, ActionBarSlot* hotBarSlotData, NumberArrayData* numberArray, StringArrayData* stringArray, int numberArrayIndex, int stringArrayIndex)
    {
        //this.logger.Debug($"OnHotBarSlotUpdate addon:{addon->NameString}, numberArrayDataPtr:0x{(nint)numberArray:X} numberArrayIndex:{numberArrayIndex}, stringArrayIndex:{stringArrayIndex}");
        try
        {
            ProcessHotBarSlot(hotBarSlotData, numberArray, numberArrayIndex);
        }
        catch(Exception e)
        {
            this.logger.Error(e.ToStringFull());
        }

        onHotBarSlotUpdateHook.Original(addon, hotBarSlotData, numberArray, stringArray, numberArrayIndex, stringArrayIndex);
    }

    private Lumina.Excel.Sheets.Action? GetAction(uint actionId)
    {
        var adjustedActionId = ActionManager.Instance()->GetAdjustedActionId(actionId);

        if (this.actionCache.TryGetValue(adjustedActionId, out var action)) { return action; }

        action = this.dalamud.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(adjustedActionId);
        this.actionCache.Add(adjustedActionId, action);
        return action;
    }

    private void ProcessHotBarSlot(ActionBarSlot* hotBarSlotData, NumberArrayData* numberArray, int numberArrayIndex)
    {
        var numberArrayData = (NumberArrayStruct*)(&numberArray->IntArray[numberArrayIndex]);

        if (numberArrayData->ActionType is not (NumberArrayActionType.Action or NumberArrayActionType.CraftAction))
        {
            return;
        }

        ApplyDarkening(hotBarSlotData, DisableAllActions);
    }

    private void ApplyDarkening(ActionBarSlot* hotBarSlotData, bool darken)
    {
        if (hotBarSlotData is null) { return; }
        var iconComponent = (AtkComponentIcon*)hotBarSlotData->Icon->Component;

        if (iconComponent is null) { return; }
        if (iconComponent->IconImage is null) { return; }
        if (iconComponent->Frame is null) { return; }

        var iconIsAlreadyDarkened =
            iconComponent->IconImage->MultiplyRed < 0x64 &&
            iconComponent->IconImage->MultiplyGreen < 0x64 &&
            iconComponent->IconImage->MultiplyBlue < 0x64;

        if (darken && !iconIsAlreadyDarkened)
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
    private T* GetUnitBase<T>(string name = null, int index = 1) where T : unmanaged
    {
        if (string.IsNullOrEmpty(name))
        {
            var attr = (AddonAttribute)typeof(T).GetCustomAttribute(typeof(AddonAttribute));
            if (attr != null)
            {
                name = attr.AddonIdentifiers.FirstOrDefault();
            }
        }

        if (string.IsNullOrEmpty(name)) { return null; }

        return (T*)this.dalamud.GameGui.GetAddonByName(name, index).Address;
    }
}
