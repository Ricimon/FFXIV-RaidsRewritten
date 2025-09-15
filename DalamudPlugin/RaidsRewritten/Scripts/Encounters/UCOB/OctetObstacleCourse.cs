using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class OctetObstacleCourse : Mechanic
{
    private const uint GrandOctetCastId = 9959;
    private const uint MegaflareDive = 9953;
    private readonly Vector3 ArenaCenter = new(0, 0, 0);

    private readonly List<Entity> attacks = [];
    public int RngSeed { get; set; }

    public override void Reset()
    {
        foreach(var attack in this.attacks)
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

    public override void OnStartingCast(Lumina.Excel.Sheets.Action action, IBattleChara source)
    {
        if (action.RowId == GrandOctetCastId)
        {
            if (AttackManager.TryCreateAttackEntity<OctetDonut>(out var donut))
            {
                var random = new Random(RngSeed);
                donut.Set(new Position(ArenaCenter))
                    .Set(new OctetDonut.SeededRandom(random));
                attacks.Add(donut);
            }
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId == MegaflareDive)
        {
            DelayedAction.Create(World, () =>
            {
                Reset();
            }, 0.5f);
        }
    }
}
