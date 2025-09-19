using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;
using System.Numerics;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class CircleOmen : IAttack
{
    public static bool IsInOmen(Entity omen, Vector3 position)
    {
        if (!omen.TryGet<Position>(out var p)) { return false; }
        if (!omen.TryGet<Scale>(out var s)) { return false; }

        var centerV2 = p.Value.ToVector2();
        var positionV2 = position.ToVector2();
        var distance = Vector2.Distance(centerV2, positionV2);

        return distance <= s.Value.X;
    }

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/general_1bf.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Add<Attack>()
            .Add<Omen>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }
}
