using Flecs.NET.Core;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class DeepFreeze(ILogger logger)
{
    public record struct Component(object _);
    private readonly ILogger logger = logger;
    public static void ApplyToPlayer(Entity tempEntity, float duration)
    {
        //Temperature.Component c = tempEntity.Get<Temperature.Component>();
        string str = "Deepfreeze";

        tempEntity.CsWorld().Entity(str)
            .Set(new Condition.Component(str, duration))
            .Set(new Component())
            .ChildOf(tempEntity);
    }
}