using System.Numerics;
using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Conditions;

public class FireResistanceDown
{
    public struct Component;

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.FireResistanceDown, extendDuration, overrideExistingDuration);
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

            var condition = Condition.ApplyToTarget(target, "Fire Resistance Down", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.FireResistanceDown))
                .Set(new Condition.StatusIconReplacement("fire_resistance_down", ConditionTable.IconToReplace.FireResistanceDown))
                .Set(new Condition.Status(ConditionTable.IconToReplace.FireResistanceDown, "Fire Resistance Down", "Fire resistance is significantly reduced."))
                .Set(new Condition.StatusTooltip("Fire Resistance Down (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>()
                .Add<Component>();
        }, 0, true).ChildOf(target);
    }
}
