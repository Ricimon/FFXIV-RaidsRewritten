using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class KnockedBack(ILogger logger) : ISystem
{
    public record struct Component(Vector3 KnockbackDirection);

    private readonly ILogger logger = logger;

    public static void ApplyToPlayer(Entity playerEntity, Vector3 knockbackDirection, float duration)
    {
        playerEntity.CsWorld().Entity()
            .Set(new Condition("Knocked Back", duration))
            .Set(new Component(knockbackDirection))
            .ChildOf(playerEntity);
    }

    public void Register(World world)
    {
        world.System<Condition, Component>()
            .Each((Iter it, int i, ref Condition condition, ref Component component) =>
            {
                try
                {
                    condition.TimeRemaining = Math.Max(condition.TimeRemaining - it.DeltaTime(), 0);

                    if (condition.TimeRemaining <= 0)
                    {
                        var e = it.Entity(i);
                        e.Destruct();
                    }
                }
                catch(Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }
}
