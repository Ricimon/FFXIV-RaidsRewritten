using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Sleep
{
    private const string IconId = "215013";

    public record struct Component(object _);

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.Sleep, extendDuration, overrideExistingDuration);
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

            var condition = Condition.ApplyToTarget(target, "Slept", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.Sleep))
                .Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Sleep))
                .Set(new Condition.Status(ConditionTable.IconToReplace.Sleep, "Sleep", "Overwhelming drowsiness is preventing the execution of actions."))
                .Set(new Condition.StatusTooltip("Sleep (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            // Application VFX
            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05ht_slp0s.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());

                DelayedAction.Create(world, (ref Iter it) =>
                {
                    // Looped VFX
                    it.World().Entity()
                        .Set(new ActorVfx("vfx/common/eff/dk10ht_slp0h.avfx"))
                        .ChildOf(condition);
                }, 0.6f).ChildOf(condition);
            }
        }, 0, true).ChildOf(target);
    }
}
