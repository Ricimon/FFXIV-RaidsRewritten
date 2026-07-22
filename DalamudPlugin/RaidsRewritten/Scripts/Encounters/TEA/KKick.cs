using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class KKick : Mechanic
{
    private const float OmenDuration = 1.1f;

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
        // Transition to BJ/CC
        if ((uint)a3 == 2147483649)
        {

        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == 18516) // J Kick
        {
            var position = set.Source.Position;

            if (EntityManager.TryCreateEntity<KnockbackOmen>(out var omen))
            {
                omen.Set(new Position(position))
                    .Set(new Scale(new Vector3(30f)))
                    .Set(new OmenDuration(OmenDuration, true));
                attacks.Add(omen);

                var action = DelayedAction.Create(World, () =>
                {
                    var player = Dalamud.ObjectTable.LocalPlayer;
                    if (player == null || player.IsDead) { return; }

                    var knockbackDirection = player.Position - position;
                    knockbackDirection.Y = 0;
                    if (knockbackDirection.LengthSquared() == 0)
                    {
                        var randomAngle = (float)(new Random().NextDouble() * 2 * Math.PI);
                        knockbackDirection = new Vector3(MathF.Cos(randomAngle), 0, MathF.Sin(randomAngle));
                    }

                    CommonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                    {
                        Knockback.ApplyToTarget(e, knockbackDirection, 2.5f, true);
                    });
                }, OmenDuration);
                attacks.Add(action);
            }
        }
    }
}
