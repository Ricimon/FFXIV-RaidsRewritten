using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class KnockedBack(PlayerManager playerManager, ILogger logger) : ISystem
{
    public record struct Component(Vector3 KnockbackDirection);

    private readonly PlayerManager playerManager = playerManager;
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

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component _) =>
            {
                if (!playerManager.IsMovementAllowedByGame)
                {
                    var e = it.Entity(i);
                    e.Destruct();
                }
            });
    }
}
