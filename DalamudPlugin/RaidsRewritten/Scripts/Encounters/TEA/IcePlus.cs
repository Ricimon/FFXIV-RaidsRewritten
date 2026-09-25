using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class IcePlus : Mechanic
{
    public int RngSeed { get; set; }

    private const uint GelidGaolBaseId = 0x2C81;
    private const uint PropellerWindActionId = 18482;
    private const string AttackVfx = "vfx/monster/gimmick2/eff/e3d7_b3_g3_c0v.avfx";

    private readonly List<Entity> attacks = [];

    private Vector3? icePosition;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        icePosition = null;
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
        if (newObject != null && newObject.BaseId == GelidGaolBaseId)
        {
            icePosition = newObject.Position;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId == PropellerWindActionId)
        {
            if (icePosition == null) { return; }

            StartAttack(icePosition.Value);
        }
    }

    public override void DebugSimulate()
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player != null)
        {
            StartAttack(player.Position);
        }
    }

    private void StartAttack(Vector3 position)
    {
        var seed = RngSeed;
        unchecked
        {
            seed += 0x1CE;
        }
        var random = new Random(seed);
        var rotationOffset = random.Next(2) == 0 ? 0 : (0.125f * MathF.PI);
        var omenDuration = 3.0f;

        var lines = new List<Entity>();
        for (var i = 0; i < 4; i++)
        {
            if (EntityManager.TryCreateEntity<LineOmen>(out var line))
            {
                line.Set(new Position(position));
                line.Set(new Scale(new Vector3(2, 1, 50)));
                line.Set(new Rotation(rotationOffset + i * 0.25f * MathF.PI));
                line.Set(new OmenDuration(omenDuration, false));
                lines.Add(line);
                attacks.Add(line);
            }
        }

        var action = DelayedAction.Create(World, () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var v = FakeActor.Create(World)
                    .Set(new ActorVfx(AttackVfx))
                    .Set(new Position(position))
                    .Set(new Rotation(rotationOffset + i * 0.25f * MathF.PI));
                attacks.Add(v);
            }

            var player = Dalamud.ObjectTable.LocalPlayer;
            if (player == null || player.IsDead) { return; }
            if (lines.AsValueEnumerable().Any(l => LineOmen.IsInOmen(l, player.Position)))
            {
                if (player.HasTranscendance())
                {
                    VfxSpawn.PlayInvulnerabilityEffect(player);
                }
                else
                {
                    CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                    {
                        Pacify.ApplyToTarget(e, 60.0f);
                    });
                }
            }
        }, omenDuration);
        attacks.Add(action);
    }
}
