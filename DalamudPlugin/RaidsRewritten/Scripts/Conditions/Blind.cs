using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Conditions;

public class Blind
{
    public const int Id = 15;
    private const int IconId = 215012;
    private const int IconToReplace = 210258;

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var condition = Condition.ApplyToTarget(target, "Blind", duration, Id, extendDuration, overrideExistingDuration);

            condition.Set(new Condition.StatusIconReplacement(IconId, IconToReplace))
                .Set(new Condition.Status(IconToReplace, "Blind", "(RaidsRewritten) Encroaching darkness is lowering visibility."))
                .Add<Condition.StatusEnfeeblement>();

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());
            }
        }, 0, true).ChildOf(target);
    }
}
