using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
namespace RaidsRewritten.Scripts.Conditions;

public class Flattened
{
    private const string IconId = "215381";

    public record struct Component(bool OriginalSet = false, float OriginalZ = 1.0f, FFXIVClientStructs.FFXIV.Common.Math.Quaternion OriginalRotation = default);
    public record struct FallingOff(float OriginalZ = 1.0f, FFXIVClientStructs.FFXIV.Common.Math.Quaternion OriginalRotation = default, float ElapsedTime = 0.15f);
    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, ConditionTable.Id.Flattened, extendDuration, overrideExistingDuration);
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

            var condition = Condition.ApplyToTarget(target, "Flattened", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());

                condition
                    //.Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Flattened))
                    .Set(new Condition.Status(ConditionTable.IconToReplace.Flattened, "Flattened", "Flat as a pancake. Unable to execute actions."))
                    .Set(new Condition.StatusTooltip("Flattened (RaidsRewritten)"))
                    .Add<Condition.StatusEnfeeblement>();

                // Application VFX
                world.Entity()
                    .Set(new ActorVfx("vfx/common/eff/toad_smk0f.avfx"))
                    .ChildOf(condition);
            }
        }, 0, true).ChildOf(target);
    }
}
