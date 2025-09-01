using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Sleep
{
    public record struct Component(object _);

    public static Entity ApplyToTarget(Entity playerEntity, float duration, int id = 0)
    {
        var world = playerEntity.CsWorld();
        var entity = Condition.ApplyToTarget(playerEntity, "Slept", duration, id);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk05ht_slp0s.avfx"))
            .ChildOf(entity);
        DelayedAction.Create(world,
            () =>
            {
                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/dk10ht_slp0h.avfx"))
                    .ChildOf(entity);
            },
            0.6f).ChildOf(entity);

        return entity;
    }

    public static bool IsSlept(Entity playerEntity)
    {
        var ret = false;
        playerEntity.Children(e =>
        {
            if (e.Has<Component>()) { ret = true; }
        });
        return ret;
    }
}
