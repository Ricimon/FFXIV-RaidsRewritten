using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

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

            condition.Set(new Condition.Status(215012, "Blind", "Encroaching darkness is lowering visibility.")).Add<Condition.StatusEnfeeblement>();

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());
            }
        }, 0, true).ChildOf(target);
    }
}
