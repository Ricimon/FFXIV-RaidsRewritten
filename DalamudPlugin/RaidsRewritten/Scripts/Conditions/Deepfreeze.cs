using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Deepfreeze
{
    public const int Id = 0xF2EE2E;
    private const int IconId = 215637;
    private const int IconToReplace = 210259;

    public record struct Component(object _);

    public static void ApplyToTarget(Entity target, float duration)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = target.CsWorld();
            var entity = Condition.ApplyToTarget(target, "Frozen", duration, Id, false, false);

            entity.Set(new Condition.StatusIconReplacement(IconId, IconToReplace))
                .Set(new Condition.Status(IconToReplace, "Deep Freeze", "Frozen solid and unable to execute actions."))
                .Set(new Condition.StatusTooltip("Deep Freeze (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            if (!entity.Has<Component>())
            {
                entity.Set(new Component());

                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/hyouketu0f.avfx"))
                    .ChildOf(entity);
            }
        }, 0, true).ChildOf(target);
    }
}
