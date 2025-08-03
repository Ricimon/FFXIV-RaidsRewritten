using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

internal class Overheat
{
    public record struct Component(object _);

    public static Entity ApplyToPlayer(Entity playerEntity, float duration, int id = 0)
    {
        var world = playerEntity.CsWorld();
        var entity = Condition.ApplyToPlayer(playerEntity, "Overheated", duration, id);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());
        }

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk10ht_hea0s.avfx"))
            .ChildOf(entity);
        return entity;
    }
}
