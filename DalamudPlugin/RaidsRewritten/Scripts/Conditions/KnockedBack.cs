using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class KnockedBack(ILogger logger) : ISystem
{
    public record struct Component(Vector3 KnockbackDirection, float TimeRemaining);

    private readonly ILogger logger = logger;

    public void Register(World world)
    {
        world.Component<Component>().IsA<Condition>();

        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                try
                {
                    component.TimeRemaining = Math.Max(component.TimeRemaining - it.DeltaTime(), 0);

                    if (component.TimeRemaining <= 0)
                    {
                        var e = it.Entity(i);
                        e.Remove<Component>();
                    }
                }
                catch(Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
