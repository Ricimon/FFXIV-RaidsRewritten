using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Conditions;

public class Heavy
{
    public const int Id = 0x8EA11;
    private const int IconId = 215002;
    private const int IconToReplace = 210260;

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();

            var condition = Condition.ApplyToTarget(target, "Slowed", duration, Id, extendDuration, overrideExistingDuration);

            condition.Set(new Condition.StatusIconReplacement(IconId, IconToReplace))
                .Set(new Condition.Status(IconToReplace, "Heavy", "(RaidsRewritten) Movement speed is reduced."))
                .Add<Condition.StatusEnfeeblement>();

            // Application VFX
            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05ht_grv0h.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());

                DelayedAction.Create(world, (ref Iter it) =>
                {
                    // Looped VFX
                    it.World().Entity()
                        .Set(new ActorVfx("vfx/common/eff/dk10ht_grv0h.avfx"))
                        .ChildOf(condition);
                }, 0.6f).ChildOf(condition);
            }
        }, 0, true).ChildOf(target);
    }
}
