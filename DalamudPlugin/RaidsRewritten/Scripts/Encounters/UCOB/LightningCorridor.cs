using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class LightningCorridor : Mechanic
{
    private const string ChainLightningVfxPath = "vfx/monster/gimmick/eff/crystal2_chein_kakusan_c0c.avfx";
    private const uint ChainLightningApplicationActionRowId = 9927;
    private const uint ChainLightningResolveActionRowId = 9928;

    private readonly List<Entity> attacks = [];

    //private record struct LightningVfxInformation(Vector3 Position, DateTime Time);
    //private LightningVfxInformation lastLightningVfx;

    private record struct ChainLightningApplication(IGameObject Target1, IGameObject Target2, DateTime Time);
    private ChainLightningApplication lastChainLightningApplication;
    private DateTime lastChainLightningResolveTime;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
        this.lastChainLightningApplication = default;
        this.lastChainLightningResolveTime = default;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action?.RowId == ChainLightningApplicationActionRowId)
        {
            var currentTime = this.Dalamud.Framework.LastUpdateUTC;
            if (set.TargetEffects != null)
            {
                var t1 = this.Dalamud.ObjectTable.SearchById(set.TargetEffects[0].TargetID);
                var t2 = this.Dalamud.ObjectTable.SearchById(set.TargetEffects[1].TargetID);
                if (t1 != null && t2 != null)
                {
                    this.lastChainLightningApplication = new ChainLightningApplication(t1, t2, currentTime);
                }
            }
        }
        else if (set.Action?.RowId == ChainLightningResolveActionRowId)
        {
            var currentTime = this.Dalamud.Framework.LastUpdateUTC;
            // Expect 2 lightning resolve actions very quickly next to each other.
            // This may be skipped if a player with lightning dies before the debuff expires.
            // The lightning debuff is also ~5 seconds, so use a 7 second cap to account for latency
            if (currentTime - this.lastChainLightningResolveTime < TimeSpan.FromSeconds(0.5f) &&
                currentTime - this.lastChainLightningApplication.Time < TimeSpan.FromSeconds(7.0f) &&
                this.lastChainLightningApplication.Target1 != null &&
                this.lastChainLightningApplication.Target2 != null)
            {
                var p1 = this.lastChainLightningApplication.Target1.Position;
                var p2 = this.lastChainLightningApplication.Target2.Position;
                var p = (p1 + p2) * 0.5f;
                var r = MathUtilities.VectorToRotation(p2.ToVector2() - p1.ToVector2());

                if (this.EntityManager.TryCreateEntity<Attacks.LightningCorridor>(out var attack))
                {
                    attack.Set(new Position(p)).Set(new Rotation(r));
                }

                // Consume the application
                this.lastChainLightningApplication = default;
            }

            this.lastChainLightningResolveTime = currentTime;
        }
    }

    // There's no VFXSpawn callback for the chain lightning VFX
    //public override void OnVFXSpawn(IGameObject? target, string vfxPath)
    //{
    //    if (target == null) { return; }
    //    if (vfxPath != ChainLightningVfxPath) { return; }

    //    var currentTime = this.Dalamud.Framework.LastUpdateUTC;
    //    if (currentTime - lastLightningVfx.Time < TimeSpan.FromSeconds(1))
    //    {
    //        var p1 = lastLightningVfx.Position;
    //        var p2 = target.Position;
    //        var p = (p1 + p2) * 0.5f;
    //        var r = MathUtilities.VectorToRotation(p2.ToVector2() - p1.ToVector2());

    //        if (this.AttackManager.TryCreateAttackEntity<Attacks.LightningCorridor>(out var attack))
    //        {
    //            attack.Set(new Position(p)).Set(new Rotation(r));
    //        }

    //        lastLightningVfx = default;
    //    }
    //    else
    //    {
    //        lastLightningVfx = new(target.Position, currentTime);
    //    }
    //}
}
