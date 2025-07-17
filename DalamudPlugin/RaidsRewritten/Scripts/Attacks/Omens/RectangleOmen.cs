using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class RectangleOmen : IAttack
{
    public static bool IsInOmen(Entity omen, Vector3 position)
    {
        if (!omen.TryGet<Position>(out var p)) { return false; }
        if (!omen.TryGet<Rotation>(out var r)) { return false; }
        if (!omen.TryGet<Scale>(out var s)) { return false; }

        var forward = MathUtilities.RotationToUnitVector(r.Value);
        var right = MathUtilities.RotationToUnitVector(r.Value - 0.5f * MathF.PI);

        var length = s.Value.Z;
        var width = 2 * s.Value.X;

        var originToPosition = position.ToVector2() - p.Value.ToVector2();
        var amountForward = Vector2.Dot(forward, originToPosition);
        var amountRight = Vector2.Dot(right, originToPosition);

        if (amountForward < 0) { return false; }
        if (amountForward > length) { return false; }
        if (amountRight < -0.5f * width) { return false; }
        if (amountRight > 0.5f * width) { return false; }

        return true;
    }

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/general02f.avfx"))
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
