using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class DamageDown
{
    public record struct Component;

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.DamageDown, extendDuration, overrideExistingDuration);
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

            var condition = Condition.ApplyToTarget(target, "Damage Down", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.DamageDown))
                .Set(new Condition.StatusIconReplacement("damage_down", ConditionTable.IconToReplace.DamageDown))
                .Set(new Condition.Status(ConditionTable.IconToReplace.DamageDown, "Damage Down", "Unable to use attack-oriented abilities, spells, and weaponskills."))
                .Set(new Condition.StatusTooltip("Damage Down (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05th_stdn0t.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Add<Component>();
            }
        }, 0, true).ChildOf(target);
    }
}
