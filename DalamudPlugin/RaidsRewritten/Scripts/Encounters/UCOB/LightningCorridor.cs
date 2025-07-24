using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class LightningCorridor : Mechanic
{
    private const string ChainLightningVfxPath = "vfx/monster/gimmick/eff/crystal2_chein_kakusan_c0c.avfx";

    private readonly List<Entity> attacks = [];

    private record struct LightningVfxInformation(Vector3 Position, DateTime Time);
    private LightningVfxInformation lastLightningVfx;

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
        lastLightningVfx = default;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnVFXSpawn(IGameObject? target, string vfxPath)
    {
        if (target == null) { return; }
        if (vfxPath != ChainLightningVfxPath) { return; }

        var currentTime = this.Dalamud.Framework.LastUpdateUTC;
        if (currentTime - lastLightningVfx.Time < TimeSpan.FromSeconds(1))
        {
            var p1 = lastLightningVfx.Position;
            var p2 = target.Position;
            var p = (p1 + p2) * 0.5f;
            var r = MathUtilities.VectorToRotation(p2.ToVector2() - p1.ToVector2());

            if (this.AttackManager.TryCreateAttackEntity<Attacks.LightningCorridor>(out var attack))
            {
                attack.Set(new Position(p)).Set(new Rotation(r));
            }

            lastLightningVfx = default;
        }
        else
        {
            lastLightningVfx = new(target.Position, currentTime);
        }
    }
}
