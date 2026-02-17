using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Deepfreeze
{
    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, int id = 0)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = target.CsWorld();
            var entity = Condition.ApplyToTarget(target, "Frozen", duration, id, false, false);

            entity.Set(new Condition.Status(215637, "Deep Freeze", "Frozen solid and unable to execute actions.")).Add<Condition.StatusEnfeeblement>();

            if (!entity.Has<Component>())
            {
                entity.Set(new Component());

                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/hyouketu0f.avfx"))
                    .ChildOf(entity);
            }
        }, 0, true).ChildOf(target);
    }
}
