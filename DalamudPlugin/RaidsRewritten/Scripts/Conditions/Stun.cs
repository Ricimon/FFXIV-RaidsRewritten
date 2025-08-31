using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Stun
{
    public record struct Component(object _);

    public static Entity ApplyToPlayer(Entity playerEntity, float duration, int id = 0, bool extendDuration = false)
    {
        var world = playerEntity.CsWorld();
        var entity = Condition.ApplyToPlayer(playerEntity, "Stunned", duration, id, extendDuration);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk05ht_sta0h.avfx"))
            .ChildOf(entity);
        DelayedAction.Create(world,
            () =>
            {
                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/dk10ht_sta0f.avfx"))
                    .ChildOf(entity);
            },
            0.6f).ChildOf(entity);

        return entity;
    }

    public static bool IsStunned(Entity playerEntity)
    {
        var ret = false;
        playerEntity.Children(e =>
        {
            if (e.Has<Component>()) { ret = true; }
        });
        return ret;
    }
}
