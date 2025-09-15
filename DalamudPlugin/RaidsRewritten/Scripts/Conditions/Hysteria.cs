using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Hysteria(Random random, ILogger logger) : ISystem
{
    public record struct Component(float RedirectionInterval,
        float TimeUntilRedirection = 0, Vector3 MoveDirection = default);

    public static Entity ApplyToTarget(Entity target, float duration, float redirectionInterval, int id = 0)
    {
        var world = target.CsWorld();
        var entity = Condition.ApplyToTarget(target, "Hysteria", duration, id);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component(redirectionInterval));
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk05th_stdn0t.avfx"))
            .ChildOf(entity);

        return entity;
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
