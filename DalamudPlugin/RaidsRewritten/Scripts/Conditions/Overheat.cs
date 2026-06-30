using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Overheat
{
    private const string IconId = "214278";

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = target.CsWorld();
            var entity = Condition.ApplyToTarget(target, "Overheated", duration, ConditionTable.Id.Overheat, false, false);

            entity.Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Overheat))
                .Set(new Condition.Status(ConditionTable.IconToReplace.Overheat, "Overheated", "Body is overheated, forcing forward movement."))
                .Set(new Condition.StatusTooltip("Overheated (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            if (!entity.Has<Component>())
            {
                entity.Set(new Component());

                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/dk10ht_hea0s.avfx"))
                    .ChildOf(entity);
            }
        }, 0, true).ChildOf(target);
    }
}
