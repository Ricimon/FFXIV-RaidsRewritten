using System.Collections.Generic;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.UCOB;

public class JumpableShockwaves : Mechanic
{
    private static readonly Dictionary<uint, float> HookedActions = new()
    {
        {9941, 0.7f},  // Flatten
        {9962, 0.7f},  // Akh Morn (big hit)
        {9963, 0.7f},  // Akh Morn (small hit)
        {9964, 0.7f},  // Morn Afah
        {9965, 0.7f},  // Morn Afah (enrage hit 1)
        {9966, 0.7f},  // Morn Afah (enrage hit 2+)
    };

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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Target == null) { return; }
        if (!HookedActions.TryGetValue(set.Action.Value.RowId, out var delay)) { return; }

        var da = DelayedAction.Create(this.World, () =>
        {
            if (this.EntityManager.TryCreateEntity<JumpableShockwave>(out var jumpwave))
            {
                var position = set.Target.Position;
                position.Y = 0;
                jumpwave.Set(new Position(position))
                    .Set(new Rotation());
                this.attacks.Add(jumpwave);
            }
        }, delay);
        this.attacks.Add(da);
    }
}
