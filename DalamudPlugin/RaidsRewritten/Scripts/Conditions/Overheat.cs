using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Overheat
{
    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, int id = 0)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = target.CsWorld();
            var entity = Condition.ApplyToTarget(target, "Overheated", duration, id, false, false);

            entity.Set(new Condition.Status(214278, "Overheated", "Body is overheated, forcing forward movement.")).Add<Condition.StatusEnfeeblement>();

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
