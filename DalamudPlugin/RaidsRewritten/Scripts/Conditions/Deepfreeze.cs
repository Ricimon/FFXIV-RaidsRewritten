using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Deepfreeze
{
    public record struct Component(object _);

    public static Entity ApplyToPlayer(Entity playerEntity, float duration, int id = 0)
    {
        var world = playerEntity.CsWorld();
        var entity = Condition.ApplyToPlayer(playerEntity, "Frozen", duration, id);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/hyouketu0f.avfx"))
            .ChildOf(entity);
        return entity;
    }
}
