using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Stun
{
    private const string IconId = "215004";

    public record struct Component(object _);

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.Stun, extendDuration, overrideExistingDuration);
    }

    public static void ApplyToTarget(
        Entity target,
        float duration,
        BigInteger id,
        bool extendDuration = false,
        bool overrideExistingDuration = false,
        bool isClientControlled = true)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();

            var condition = Condition.ApplyToTarget(target, "Stunned", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.Stun))
                .Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Stun))
                .Set(new Condition.Status(ConditionTable.IconToReplace.Stun, "Stun", "Unable to execute actions."))
                .Set(new Condition.StatusTooltip("Stun (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            // Application VFX
            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05ht_sta0h.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());

                DelayedAction.Create(world, (ref Iter it) =>
                {
                    // Looped VFX
                    it.World().Entity()
                        .Set(new ActorVfx("vfx/common/eff/dk10ht_sta0f.avfx"))
                        .ChildOf(condition);
                }, 0.6f).ChildOf(condition);
            }
        }, 0, true).ChildOf(target);
    }
}
