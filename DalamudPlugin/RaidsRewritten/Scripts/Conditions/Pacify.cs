using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Pacify
{
    public const int Id = 0x9AC1F1;

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();

            var condition = Condition.ApplyToTarget(target, "Pacified", duration, Id, extendDuration, overrideExistingDuration);

            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05ht_ipws0t.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());
            }
        }, 0, true);
    }
}
