using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Pacify
{
    public record struct Component(object _);

    public static Entity ApplyToTarget(Entity target, float duration, int id = 0)
    {
        var world = target.CsWorld();
        var entity = Condition.ApplyToPlayer(target, "Pacified", duration, id);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk05ht_ipws0t.avfx"))
            .ChildOf(entity);

        return entity;
    }
}
