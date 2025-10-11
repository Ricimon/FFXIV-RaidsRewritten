// Adapted from https://github.com/awgil/ffxiv_bossmod/blob/master/BossMod/Framework/ActionManagerEx.cs
// 4c1a83c
using System;
using System.Numerics;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using RaidsRewritten.Data;
using RaidsRewritten.Log;

namespace RaidsRewritten.Interop;

public unsafe sealed class ActionManagerEx : IDisposable
{
    public bool DisableAllActions { get; set; }
    public bool DisableDamagingActions { get; set; }

    private readonly DalamudServices dalamud;
    private readonly ILogger logger;

    private readonly Hook<ActionManager.Delegates.Update> updateHook;
    private readonly Hook<ActionManager.Delegates.UseAction> useActionHook;
    private readonly Hook<ActionManager.Delegates.UseActionLocation> useActionLocationHook;

    private DateTime nextAllowedCancelCast;

    public ActionManagerEx(DalamudServices dalamud, ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        dalamud.GameInteropProvider.InitializeFromAttributes(this);

        var hook = dalamud.GameInteropProvider;

        this.updateHook = hook.HookFromAddress<ActionManager.Delegates.Update>(ActionManager.Addresses.Update.Value, UpdateDetour);
        this.useActionHook = hook.HookFromAddress<ActionManager.Delegates.UseAction>(ActionManager.Addresses.UseAction.Value, UseActionDetour);
        this.useActionLocationHook = hook.HookFromAddress<ActionManager.Delegates.UseActionLocation>(ActionManager.Addresses.UseActionLocation.Value, UseActionLocationDetour);
        this.updateHook.Enable();
        this.useActionHook.Enable();
        this.useActionLocationHook.Enable();
    }

    public void Dispose()
    {
        this.updateHook.Dispose();
        this.useActionHook.Dispose();
        this.useActionLocationHook.Dispose();
    }

    public void CancelCast(bool onlyDamagingActions)
    {
        // Since the game API is sending a packet, there is some rate limiting here.
        var currentTime = this.dalamud.Framework.LastUpdateUTC;
        if (currentTime < this.nextAllowedCancelCast) { return; }

        var localPlayer = this.dalamud.ClientState.LocalPlayer;
        if (localPlayer != null &&
            localPlayer.IsCasting)
        {
            if (onlyDamagingActions && !Actions.DamageActions.Contains(localPlayer.CastActionId))
            {
                return;
            }
            UIState.Instance()->Hotbar.CancelCast();
            this.nextAllowedCancelCast = currentTime.AddSeconds(0.2f);
        }
    }

    private void UpdateDetour(ActionManager* self)
    {
        updateHook.Original(self);

        // Autos are allowed if damaging actions are disabled, because Paladin
        var autosEnabled = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
        if (DisableAllActions && autosEnabled)
        {
            self->UseAction(ActionType.GeneralAction, 1);
        }
    }

    // note: targetId is usually your current primary target (or 0xE0000000 if you don't target anyone), unless you do something like /ac XXX <f> etc
    private bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        if (DisableAllActions)
        {
            // Allow auto cancellation
            var autosEnabled = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
            if (!autosEnabled) { return false; }
            var isStopAutosAction = actionType == ActionType.GeneralAction && actionId == 1;
            if (!isStopAutosAction) { return false; }
        }

        if (DisableDamagingActions)
        {
            if (Actions.DamageActions.Contains(actionId)) { return false; }
        }

        var res = useActionHook.Original(self, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        this.logger.Debug($"USE_ACTION: type:{actionType}, actionId:{actionId}, targetId:{targetId}, mode:{mode}, comboRouteId:{comboRouteId}, ret:{res}");
        return res;
    }

    private bool UseActionLocationDetour(ActionManager* self, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam, byte a7)
    {
        if (DisableAllActions)
        {
            // Allow auto cancellation
            var autosEnabled = UIState.Instance()->WeaponState.AutoAttackState.IsAutoAttacking;
            if (!autosEnabled) { return false; }
            var isStopAutosAction = actionType == ActionType.GeneralAction && actionId == 1;
            if (!isStopAutosAction) { return false; }
        }

        if (DisableDamagingActions)
        {
            if (Actions.DamageActions.Contains(actionId)) { return false; }
        }

        //var targetSystem = TargetSystem.Instance();
        //var player = GameObjectManager.Instance()->Objects.IndexSorted[0].Value;
        //var prevSeq = _inst->LastUsedActionSequence;
        //var prevRot = player != null ? player->Rotation.Radians() : default;
        //var hardTarget = targetSystem->Target;
        //var preventAutos = _autoAutosTweak.ShouldPreventAutoActivation(ActionManager.GetSpellIdForAction(actionType, actionId));
        //if (preventAutos)
        //    targetSystem->Target = null;
        bool ret = useActionLocationHook.Original(self, actionType, actionId, targetId, location, extraParam, a7);
        this.logger.Debug($"USE_ACTION_LOCATION: type:{actionType}, actionId:{actionId}, targetId:{targetId}, location:{*location}, ret:{ret}");
        //if (preventAutos)
        //    targetSystem->Target = hardTarget;
        return ret;
    }
}
