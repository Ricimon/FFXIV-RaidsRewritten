using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class JWaveSpreads : Mechanic
{
    private const uint JWaveActionId = 18551;

    private const float SpreadRadius = 6.0f;
    private const float OmenDuration = 2.0f;
    private const float MagicVulnerabilityDuration = 10.0f;
    private const float StunDuration = 20.0f;

    private const string OmenVfxPath = "vfx/lockon/eff/lockon5_t0h.avfx";
    private const string AttackVfxPath = "vfx/monster/m0117/eff/baha_bahamut_lenge_gt_c0s.avfx";

    private readonly List<Entity> attacks = [];

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
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
        if (set.Action == null) { return; }
        if (set.Action.Value.RowId != JWaveActionId) { return; }
        if (set.Source == null) { return; }

        StartMechanic(set.Source.Position);
    }

    public override void DebugSimulate()
    {
        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null) { return; }

        StartMechanic(player.Position);
    }

    private void StartMechanic(Vector3 sourcePosition)
    {
        // Every client picks the same target since they all see the same party positions
        IPlayerCharacter? target = null;
        var closestDistance = float.MaxValue;
        CommonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component player) =>
        {
            var pc = player.PlayerCharacter;
            if (pc == null || pc.IsDead) { return; }

            var distance = Vector2.Distance(sourcePosition.ToVector2(), pc.Position.ToVector2());
            if (distance < closestDistance)
            {
                closestDistance = distance;
                target = pc;
            }
        });
        if (target == null) { return; }

        var omen = World.Entity()
            .Set(new ActorVfxSource(target))
            .Set(new ActorVfx(OmenVfxPath));
        attacks.Add(omen);

        var action = DelayedAction.Create(World, () =>
        {
            omen.SafeDestruct();
            if (!target.IsValid()) { return; }

            var attackVfx = World.Entity()
                .Set(new ActorVfxSource(target))
                .Set(new ActorVfx(AttackVfxPath));
            attacks.Add(attackVfx);

            var player = Dalamud.ObjectTable.LocalPlayer;
            if (player == null || player.IsDead) { return; }

            var distanceToTarget = Vector2.Distance(target.Position.ToVector2(), player.Position.ToVector2());
            if (distanceToTarget > SpreadRadius) { return; }

            if (player.HasTranscendance())
            {
                VfxSpawn.PlayInvulnerabilityEffect(player);
                return;
            }

            CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
            {
                using var q = World.QueryBuilder().With<MagicVulnerabilityUp.Component>().With(Ecs.ChildOf, e).Build();
                if (q.IsTrue())
                {
                    Stun.ApplyToTarget(e, StunDuration);
                }
                MagicVulnerabilityUp.ApplyToTarget(e, MagicVulnerabilityDuration, overrideExistingDuration: true);
            });
        }, OmenDuration);
        attacks.Add(action);
    }
}
