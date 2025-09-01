using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Bind
{
    public record struct Component(object _);

    public static void ApplyToTarget(Entity playerEntity, float duration)
    {
        var world = playerEntity.CsWorld();
        var e = world.Entity()
            .Set(new Condition.Component("Bound", duration))
            .Set(new Component())
            .ChildOf(playerEntity);

        world.Entity()
            .Set(new ActorVfx("vfx/common/eff/dk05ht_bind0t.avfx"))
            .ChildOf(e);
        DelayedAction.Create(world,
            () =>
            {
                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/dk10ht_bind0h.avfx"))
                    .ChildOf(e);
            },
            0.6f).ChildOf(e);
    }
}
