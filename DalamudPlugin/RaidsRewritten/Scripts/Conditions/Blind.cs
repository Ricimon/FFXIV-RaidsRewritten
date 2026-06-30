using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Conditions;

public class Blind
{
    private const string IconId = "215012";

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var condition = Condition.ApplyToTarget(target, "Blind", duration, ConditionTable.Id.Blind, extendDuration, overrideExistingDuration);

            condition.Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Blind))
                .Set(new Condition.Status(ConditionTable.IconToReplace.Blind, "Blind", "Encroaching darkness is lowering visibility."))
                .Set(new Condition.StatusTooltip("Blind (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());
            }
        }, 0, true).ChildOf(target);
    }
}
