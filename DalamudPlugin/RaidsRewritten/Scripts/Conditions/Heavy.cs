using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Heavy
{
    private const string IconId = "215002";

    public record struct Component(object _);

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.Heavy, extendDuration, overrideExistingDuration);
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

            var condition = Condition.ApplyToTarget(target, "Slowed", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.Heavy))
                .Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Heavy))
                .Set(new Condition.Status(ConditionTable.IconToReplace.Heavy, "Heavy", "Movement speed is reduced."))
                .Set(new Condition.StatusTooltip("Heavy (RaidsRewritten)"))
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
