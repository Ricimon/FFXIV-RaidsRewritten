using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class EpicHero
{
    public static void ApplyToTarget(Entity target)
    {
        ApplyToTarget(target, ConditionTable.Id.EpicHero);
    }

    public static void ApplyToTarget(
        Entity target,
        BigInteger id,
        bool isClientControlled = true)
    {
        Hero.ApplyToTarget(target, id, isClientControlled,
            "Epic Hero",
            "Epic Hero (RaidsRewritten)",
            "Dueling in a decisive battle.",
            "epic_hero",
            ConditionTable.IconToReplace.EpicHero);
    }
}

public class FatedHero
{
    public static void ApplyToTarget(Entity target)
    {
        ApplyToTarget(target, ConditionTable.Id.FatedHero);
    }

    public static void ApplyToTarget(
        Entity target,
        BigInteger id,
        bool isClientControlled = true)
    {
        Hero.ApplyToTarget(target, id, isClientControlled,
            "Fated Hero",
            "Fated Hero (RaidsRewritten)",
            "Dueling in a decisive battle.",
            "fated_hero",
            ConditionTable.IconToReplace.FatedHero);
    }
}

file class Hero
{
    public static void ApplyToTarget(
        Entity target,
        BigInteger id,
        bool isClientControlled,
        string statusName,
        string statusTooltipName,
        string statusDescription,
        string iconName,
        int iconToReplace)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();

            var condition = Condition.ApplyToTarget(target, statusName, float.PositiveInfinity, id, false, false, isClientControlled);

            condition
                .Set(new Condition.StatusIconReplacement(iconName, iconToReplace))
                .Set(new Condition.Status(iconToReplace, statusName, statusDescription))
                .Set(new Condition.StatusTooltip(statusTooltipName))
                .Add<Condition.StatusEnfeeblement>();

            // Application VFX
            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05th_stdn0t.avfx"))
                .ChildOf(condition);
        }, 0, true).ChildOf(target);
    }
}
