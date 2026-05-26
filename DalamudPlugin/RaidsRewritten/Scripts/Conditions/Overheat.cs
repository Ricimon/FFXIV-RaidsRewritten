using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Overheat
{
    public const int Id = 0x807;
    private const int IconId = 214278;
    private const int IconToReplace = 210264;

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = target.CsWorld();
            var entity = Condition.ApplyToTarget(target, "Overheated", duration, Id, false, false);

            entity.Set(new Condition.StatusIconReplacement(IconId, IconToReplace))
                .Set(new Condition.Status(IconToReplace, "Overheated", "Body is overheated, forcing forward movement."))
                .Set(new Condition.StatusTooltip("Overheated (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            if (!entity.Has<Component>())
            {
                entity.Set(new Component());

                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/dk10ht_hea0s.avfx"))
                    .ChildOf(entity);
            }
        }, 0, true).ChildOf(target);
    }
}
