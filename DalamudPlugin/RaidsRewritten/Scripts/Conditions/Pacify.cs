using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Pacify
{
    public const int Id = 0x9AC1F1;
    private const int IconId = 215017;
    private const int IconToReplace = 210265;

    public record struct Component(object _);

    public static void ApplyToTarget(
        Entity target,
        float duration,
        bool extendDuration = false,
        bool overrideExistingDuration = false)
    {
        ApplyToTarget(target, duration, Id, extendDuration, overrideExistingDuration);
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

            var condition = Condition.ApplyToTarget(target, "Pacified", duration, id, extendDuration, overrideExistingDuration, isClientControlled);

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.Pacify))
                .Set(new Condition.StatusIconReplacement(IconId, IconToReplace))
                .Set(new Condition.Status(IconToReplace, "Pacification", "Unable to use attack-oriented abilities, spells, and weaponskills."))
                .Set(new Condition.StatusTooltip("Pacification (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

            world.Entity()
                .Set(new ActorVfx("vfx/common/eff/dk05ht_ipws0t.avfx"))
                .ChildOf(condition);

            if (!condition.Has<Component>())
            {
                condition.Set(new Component());
            }
        }, 0, true).ChildOf(target);
    }
}
