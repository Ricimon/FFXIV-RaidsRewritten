using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class OneThirdDonutOmen : IAttack
{
    public static bool IsInOmen(Entity omen, Vector3 position)
    {
        if (!omen.TryGet<Position>(out var p)) { return false; }
        if (!omen.TryGet<Scale>(out var s)) { return false; }

        var centerV2 = p.Value.ToVector2();
        var positionV2 = position.ToVector2();
        var distance = Vector2.Distance(centerV2, positionV2);

        return 0.3 * s.Value.X <= distance && distance <= s.Value.X;
    }

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new StaticVfx("vfx/omen/eff/z5r2_b1_dnt_o0g.avfx"))
            .Add<Attack>()
            .Add<Omen>();
    }

    public Entity Create(World world) => CreateEntity(world);
}
