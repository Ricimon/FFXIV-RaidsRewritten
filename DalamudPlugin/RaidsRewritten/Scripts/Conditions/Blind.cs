using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Conditions;

public class Blind
{
    public const int Id = 15;

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var condition = Condition.ApplyToTarget(target, "Blind", duration, Id, extendDuration, overrideExistingDuration);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());
            }
        }, 0, true).ChildOf(target);
    }
}
