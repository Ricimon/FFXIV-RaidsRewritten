using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class KnockedBack(ILogger logger)
{
    public record struct Component(Vector3 KnockbackDirection);

    private readonly ILogger logger = logger;

    public static void ApplyToPlayer(Entity playerEntity, Vector3 knockbackDirection, float duration)
    {
        // Remove existing knockback conditions
        playerEntity.Scope(() =>
        {
            playerEntity.CsWorld().Query<Component>().Each((Entity e, ref Component _) =>
            {
                e.Destruct();
            });
        });

        playerEntity.CsWorld().Entity()
            .Set(new Condition.Component("Knocked Back", duration))
            .Set(new Component(knockbackDirection))
            .ChildOf(playerEntity);
    }
}
