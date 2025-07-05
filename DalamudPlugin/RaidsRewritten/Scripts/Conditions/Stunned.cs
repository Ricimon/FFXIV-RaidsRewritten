using Flecs.NET.Core;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class Stunned(ILogger logger)
{
    public record struct Component(object _);

    private readonly ILogger logger = logger;

    public static void ApplyToPlayer(Entity playerEntity, float duration)
    {
        playerEntity.CsWorld().Entity()
            .Set(new Condition.Component("Stunned", duration))
            .Set(new Component())
            .ChildOf(playerEntity);
    }
}
