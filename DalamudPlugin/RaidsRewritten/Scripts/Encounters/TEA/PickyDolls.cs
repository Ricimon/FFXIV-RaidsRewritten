using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class PickyDolls : Mechanic
{
    public int RngSeed { get; set; }

    private record struct Target(Entity Entity, ulong ContentId);
    private record struct Doll(Villain Villain, Entity Vfx);
    private enum Villain
    {
        Epic,
        Fated,
    }

    private const uint LIVING_LIQUID_BASE_ID = 0x2C47;
    private const uint LIQUID_HAND_BASE_ID = 0x2C48;
    private const uint JAGD_DOLL_BASE_ID = 0x2C4A;
    private const uint FLUID_SWING_ACTION_ID = 18864;
    private const uint JAGD_DOLL_AUTO_ATTACK_ID = 19278;
    private const uint REDUCIBLE_COMPLEXITY_ID = 18464;
    private const string EPIC_VILLAIN = "vfx/common/eff/z6r1_b3_stlp01_c0t1.avfx";
    private const string FATED_VILLAIN = "vfx/common/eff/z6r1_b3_stlp02_c0t1.avfx";
    //private const string FEED_FAIL = "vfx/monster/m0055/eff/m0055sp09c0c.avfx";
    private const string FEED_FAIL = "vfx/monster/gimmick3/eff/n4g7_b3_g21c0x.avfx";

    private readonly List<Entity> attacks = [];
    private readonly Dictionary<uint, Doll?> dolls = [];

    private bool debuffsAssigned = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        dolls.Clear();
        debuffsAssigned = false;

        World.DeleteWith<EpicHero.Component>();
        World.DeleteWith<FatedHero.Component>();
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
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == FLUID_SWING_ACTION_ID)
        {
            AssignDebuffs(0.75f);
        }

        if (set.Action.Value.RowId == JAGD_DOLL_AUTO_ATTACK_ID &&
            set.Source.BaseId == JAGD_DOLL_BASE_ID)
        {
            JagdDollAutoAttack(set);
        }

        if (set.Action.Value.RowId == REDUCIBLE_COMPLEXITY_ID)
        {
            RemoveDoll(set.Source.EntityId);
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.BaseId != JAGD_DOLL_BASE_ID) { return; }

        if (dolls.ContainsKey(newObject.EntityId)) { return; }
        dolls.Add(newObject.EntityId, null);

        if (dolls.Count == 4)
        {
            var epicVillainsToAssign = 2;
            var fatedVillainsToAssign = 2;
            var random = new Random(RngSeed + 53254);

            var sortedDolls = dolls.Keys.AsValueEnumerable().OrderBy(id =>
            {
                var position = Dalamud.ObjectTable.SearchByEntityId(id)?.Position ?? default;
                return position.X * 1000 + position.Z;
            }).ToList();
            foreach(var dollId in sortedDolls)
            {
                // Assign Epic/Fated Villain to Jagd Dolls
                var totalAssigns = epicVillainsToAssign + fatedVillainsToAssign;
                if (totalAssigns <= 0) { return; }

                var r = random.Next(totalAssigns);

                Villain villain;
                string vfxPath;
                if (r < epicVillainsToAssign)
                {
                    villain = Villain.Epic;
                    vfxPath = EPIC_VILLAIN;
                    epicVillainsToAssign--;
                }
                else
                {
                    villain = Villain.Fated;
                    vfxPath = FATED_VILLAIN;
                    fatedVillainsToAssign--;
                }

                var action = DelayedAction.Create(World, () =>
                {
                    var dollObject = Dalamud.ObjectTable.SearchByEntityId(dollId);
                    var vfx = World.Entity()
                        .Set(new ActorVfx(vfxPath))
                        .Set(new ActorVfxSource(dollObject));
                    attacks.Add(vfx);

                    dolls[dollId] = new(villain, vfx);
                }, 1.0f);
                attacks.Add(action);
            }
        }
    }

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        if (source.BaseId == LIVING_LIQUID_BASE_ID &&
            command == 14 && // command 14 seems to be death animation
            dolls.Count > 0)
        {
            foreach (var dollId in dolls.Keys.ToList())
            {
                // Need testing to see if doll entity still exists when Liquid dies
                var doll = Dalamud.ObjectTable.SearchByEntityId(dollId);
                if (doll != null)
                {
                    DollFeedFailure(doll);
                }
                RemoveDoll(dollId);
            }
        }
    }

    public override void OnTetherCreate(IGameObject source, IGameObject target, uint data2, uint data3, uint data5)
    {
        if (dolls.TryGetValue(source.EntityId, out var doll) &&
            doll.HasValue &&
            target.ObjectKind != ObjectKind.Pc)
        {
            var feedTarget = doll.Value.Villain == Villain.Epic ? LIVING_LIQUID_BASE_ID : LIQUID_HAND_BASE_ID;
            if (target.BaseId != feedTarget)
            {
                var action = DelayedAction.Create(World, () =>
                {
                    DollFeedFailure(source);
                }, 1.7f);
                attacks.Add(action);
            }
        }
    }

    public override void DebugSimulate()
    {
        AssignDebuffs(0.0f);
    }

    private void AssignDebuffs(float delay)
    {
        if (debuffsAssigned) { return; }
        debuffsAssigned = true;

        var action = DelayedAction.Create(World, () =>
        {
            var mainTargets = new List<Target>();
            var backupTargets = new List<Target>();
            CommonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component player) =>
            {
                if (player.PlayerCharacter == null) { return; }
                if (!e.TryGet(out Player.ContentId contentId)) { return; }
                switch (player.PlayerCharacter.GetRole())
                {
                    case CombatRole.Healer:
                    case CombatRole.DPS:
                        mainTargets.Add(new(e, contentId.Value));
                        break;
                    default:
                        backupTargets.Add(new(e, contentId.Value));
                        break;
                }
            });

            var random = new Random(RngSeed);
            mainTargets = [.. mainTargets.AsValueEnumerable().OrderBy(v => v.ContentId).OrderBy(v => random.Next())];
            backupTargets = [.. backupTargets.AsValueEnumerable().OrderBy(v => v.ContentId).OrderBy(v => random.Next())];

            // Assign targets
            var alphaAssignments = 2;
            var betaAssignments = 2;

            void EnumerateTargets(List<Target> targets)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (alphaAssignments > 0)
                    {
                        EpicHero.ApplyToTarget(targets[i].Entity);
                        alphaAssignments--;
                    }
                    else if (betaAssignments > 0)
                    {
                        FatedHero.ApplyToTarget(targets[i].Entity);
                        betaAssignments--;
                    }
                }
            }

            EnumerateTargets(mainTargets);
            EnumerateTargets(backupTargets);
        }, delay);
        attacks.Add(action);
    }

    private void JagdDollAutoAttack(ActionEffectSet set)
    {
        if (set.Target == null) { return; }
        if (dolls.TryGetValue(set.Source!.EntityId, out var doll) && doll.HasValue)
        {
            CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
            {
                if (set.Target.EntityId != pc.PlayerCharacter?.EntityId)
                {
                    return;
                }

                var applyPunishment = false;
                switch (doll.Value.Villain)
                {
                    case Villain.Epic:
                        {
                            using var q = World.QueryBuilder<EpicHero.Component>().With(Ecs.ChildOf, e).Build();
                            applyPunishment = !q.IsTrue();
                        }
                        break;
                    case Villain.Fated:
                        {
                            using var q = World.QueryBuilder<FatedHero.Component>().With(Ecs.ChildOf, e).Build();
                            applyPunishment = !q.IsTrue();
                        }
                        break;
                }

                if (applyPunishment)
                {
                    Stun.ApplyToTarget(e, 10.0f);
                    Pacify.ApplyToTarget(e, 30.0f);
                }
            });
        }
    }

    private void RemoveDoll(uint dollEntityId)
    {
        if (dolls.Remove(dollEntityId, out var doll))
        {
            doll?.Vfx.Destruct();
        }

        if (dolls.Count == 0)
        {
            World.DeleteWith<EpicHero.Component>();
            World.DeleteWith<FatedHero.Component>();
        }
    }

    private void DollFeedFailure(IGameObject doll)
    {
        var vfx = World.Entity()
            .Set(new ActorVfx(FEED_FAIL))
            .Set(new ActorVfxSource(doll));
        attacks.Add(vfx);

        CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
        {
            Hysteria.ApplyToTarget(e, 10.0f, 5.0f);
            Pacify.ApplyToTarget(e, 30.0f);
        });
    }
}
