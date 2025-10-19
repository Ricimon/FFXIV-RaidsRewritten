﻿using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class EarthShakerStar : Mechanic
{
    public const string VfxPath = "bgcommon/world/common/vfx_for_btl/b0801/eff/b0801_yuka_o.avfx";

    private const uint EarthShakerPuddleBaseId = 0x1E9663;

    private readonly List<Entity> attacks = [];

    public override void Reset()
    {
        foreach (var attack in this.attacks)
        {
            attack.Destruct();
        }
        this.attacks.Clear();
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

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.BaseId != EarthShakerPuddleBaseId) { return; }

        if (this.EntityManager.TryCreateEntity<Star>(out var star))
        {
            star.Set(new Star.Component(
                Type: Star.Type.Long,
                OmenTime: 5.0f,
                VfxPath: "vfx/monster/gimmick5/eff/x6r7_b3_g08_c0p.avfx",
                OnHit: e =>
                {
                    Stun.ApplyToTarget(e, 5.0f, true);
                    Pacify.ApplyToTarget(e, 30.0f, true);
                }));
            star.Set(new Position(newObject.Position));
            attacks.Add(star);
        }
    }
}
