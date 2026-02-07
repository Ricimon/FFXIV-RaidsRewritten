using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Hysteria(Random random, ILogger logger) : ISystem
{
    public const int Id = 0x5CA6E;

    public record struct Component(float RedirectionInterval,
        float TimeUntilRedirection = 0, Vector3 MoveDirection = default);

    public static void ApplyToTarget(Entity target, float duration, float redirectionInterval, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();

            var condition = Condition.ApplyToTarget(target, "Hysteria", duration, Id, extendDuration, overrideExistingDuration);

            condition.Set(new Condition.Status(215552, "Hysteria", "Unable to act on your own free will.")).Add<Condition.StatusEnfeeblement>();

            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05th_stdn0t.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component(redirectionInterval));
            }
        }, 0, true).ChildOf(target);
    }

    public void Register(World world)
    {
        world.System<Player.Component, Component>()
            .TermAt(0).Up()
            .With<Player.LocalPlayer>().Up()
            .Each((Iter it, int i, ref Player.Component pc, ref Component component) =>
            {
                component.TimeUntilRedirection -= it.DeltaTime();

                if (component.TimeUntilRedirection <= 0)
                {
                    component.TimeUntilRedirection += component.RedirectionInterval;

                    // Redirect
                    var randomAngle = (float)(random.NextDouble() * 2 * Math.PI);
                    component.MoveDirection = new Vector3(MathF.Cos(randomAngle), 0, MathF.Sin(randomAngle));
                }
            });
    }
}
