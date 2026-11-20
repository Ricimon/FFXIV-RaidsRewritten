using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

// The P12S tilts, here only to show which tower a beacon holder can take
public class AstralTilt
{
    public record struct Component;

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.AstralTilt, extendDuration, overrideExistingDuration);
    }

    public static void ApplyToTarget(
        Entity target,
        float duration,
        BigInteger id,
        bool extendDuration = false,
        bool overrideExistingDuration = false,
        bool isClientControlled = true)
    {
        Tilt.ApplyToTarget(target, duration, id, extendDuration, overrideExistingDuration, isClientControlled,
            "Astral Tilt",
            "Internal aether is dramatically polarized toward the astral.",
            "astral_tilt",
            ConditionTable.IconToReplace.AstralTilt,
            e => e.Add<Component>());
    }
}

public class UmbralTilt
{
    public record struct Component;

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.UmbralTilt, extendDuration, overrideExistingDuration);
    }

    public static void ApplyToTarget(
        Entity target,
        float duration,
        BigInteger id,
        bool extendDuration = false,
        bool overrideExistingDuration = false,
        bool isClientControlled = true)
    {
        Tilt.ApplyToTarget(target, duration, id, extendDuration, overrideExistingDuration, isClientControlled,
            "Umbral Tilt",
            "Internal aether is dramatically polarized toward the umbral.",
            "umbral_tilt",
            ConditionTable.IconToReplace.UmbralTilt,
            e => e.Add<Component>());
    }
}

file class Tilt
{
    public static void ApplyToTarget(
        Entity target,
        float duration,
        BigInteger id,
        bool extendDuration,
        bool overrideExistingDuration,
        bool isClientControlled,
        string name,
        string description,
        string iconName,
        int iconToReplace,
        Action<Entity> addComponent)
    {
        DelayedAction.Create(target.Stage().CsWorld(), (ref Iter it) =>
        {
            var world = it.Stage();

            var condition = Condition.ApplyToTarget(target, name, duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.StatusIconReplacement(iconName, iconToReplace))
                .Set(new Condition.Status(iconToReplace, name, description))
                .Set(new Condition.StatusTooltip($"{name} (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            addComponent(condition);

            // Application VFX
            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05th_stdn0t.avfx"))
                .ChildOf(condition);
        }, 0, true).ChildOf(target);
    }
}
