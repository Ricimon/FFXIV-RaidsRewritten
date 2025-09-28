using System.Collections.Generic;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class LimitCutNumber
{
    public const int Id = 0xC47C4;
    public record struct Component(object _);

    private static List<string> SymbolPaths = new List<string> {
        "vfx/lockon/eff/m0361trg_a1t.avfx",
        "vfx/lockon/eff/m0361trg_a2t.avfx",
        "vfx/lockon/eff/m0361trg_a3t.avfx",
        "vfx/lockon/eff/m0361trg_a4t.avfx",
        "vfx/lockon/eff/m0361trg_a5t.avfx",
        "vfx/lockon/eff/m0361trg_a6t.avfx",
        "vfx/lockon/eff/m0361trg_a7t.avfx",
        "vfx/lockon/eff/m0361trg_a8t.avfx",
    };

    public static void ApplyToTarget(Entity target, float duration, int number, bool extendDuration = false, bool overrideExistingDuration = false)
    {
        var world = target.CsWorld();
        var entity = Condition.ApplyToTarget(target, "Limit Cut", duration, Id, extendDuration, overrideExistingDuration);
        entity.Add<Condition.Hidden>();
        if (!entity.Has<Component>())
        {
            entity.Set(new Component());
            var e = world.Entity()
                .Set(new ActorVfx(SymbolPaths[number]))
                .ChildOf(entity);
        }
    }
}