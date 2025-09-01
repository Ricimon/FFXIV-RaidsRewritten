using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Heavy
{
    public record struct Component(object _);

    public static Entity ApplyToTarget(Entity playerEntity, float duration, int id = 0, bool extendDuration = false)
    {
        var world = playerEntity.CsWorld();
        var entity = Condition.ApplyToTarget(playerEntity, "Slowed", duration, id, extendDuration);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());

            DelayedAction.Create(world,
                () =>
                {
                    world.Entity()
                        .Set(new ActorVfx("vfx/common/eff/dk10ht_grv0h.avfx"))
                        .ChildOf(entity);
                },
                0.6f).ChildOf(entity);
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk05ht_grv0h.avfx"))
            .ChildOf(entity);

        return entity;
    }
}
