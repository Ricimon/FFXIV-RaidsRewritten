using Flecs.NET.Core;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class Stun(ILogger logger)
{
    public record struct Component(object _);

    public static void ApplyToPlayer(Entity playerEntity, float duration)
    {
        playerEntity.CsWorld().Entity()
            .Set(new Condition.Component("Stunned", duration))
            .Set(new Component())
            .ChildOf(playerEntity);
    }
}
