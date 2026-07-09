using System.Collections.Generic;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class SurfsUp : Mechanic
{
    private const uint SuperBlasstyChargeActionId = 19279;
    private const int ArticulatedBitModelId1 = 3256;
    private const int ArticulatedBitModelId2 = 3257;

    private readonly List<Entity> attacks = [];

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }
        if (set.Action.Value.RowId != SuperBlasstyChargeActionId) { return; }

        var bit = World.Entity()
            .Set(new Model(ArticulatedBitModelId1))
            .Set(new Position(set.Source.Position))
            .Set(new Rotation(set.Source.Rotation))
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new TimelineBase(0))
            .Add<Attack>();
        attacks.Add(bit);

        World.Entity()
            .Set(new ActorVfx("vfx/monster/m0729/eff/m729show_sp01c0t1.avfx"))
            .ChildOf(bit);
    }
}
