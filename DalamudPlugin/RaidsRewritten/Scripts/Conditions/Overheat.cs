using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Overheat
{
    public record struct Component(object _);

    public static Entity ApplyToTarget(Entity target, float duration, int id = 0)
    {
        var world = target.CsWorld();
        var entity = Condition.ApplyToTarget(target, "Overheated", duration, id, false, false);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());

            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk10ht_hea0s.avfx"))
                .ChildOf(entity);
        }

        return entity;
    }
}
