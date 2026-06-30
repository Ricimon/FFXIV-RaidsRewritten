using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class PickyDolls : Mechanic
{
    public int RngSeed { get; set; }

    private record struct Target(Entity Entity, ulong ContentId);

    private const uint FLUID_SWING_ACTION_ID = 18864;

    private readonly List<Entity> attacks = [];

    private bool debuffsAssigned = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
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
        if (set.Action == null) { return; }

        if (set.Action.Value.RowId == FLUID_SWING_ACTION_ID)
        {
            AssignDebuffs(0.75f);
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
    }

    public override void DebugSimulate()
    {
        AssignDebuffs(0.0f);
    }

    private void AssignDebuffs(float delay)
    {
        if (debuffsAssigned) { return; }
        debuffsAssigned = true;

        DelayedAction.Create(World, () =>
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
    }
}
