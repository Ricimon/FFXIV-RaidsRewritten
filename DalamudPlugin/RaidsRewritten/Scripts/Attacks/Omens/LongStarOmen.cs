using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class LongStarOmen : IAttack
{
    public const float ScaleMultiplier = 10.0f;

    public static bool IsInOmen(Entity omen, Vector3 position)
    {
        if (!omen.TryGet<Position>(out var p)) { return false; }
        if (!omen.TryGet<Rotation>(out var r)) { return false; }
        if (!omen.TryGet<Scale>(out var s)) { return false; }

        var width = 2 * s.Value.X / 5.0f;

        bool inOmen = false;

        for (var i = 0; i < 8; i++)
        {
            var rotation = r.Value + i * 0.25f * MathF.PI;
            var forward = MathUtilities.RotationToUnitVector(rotation);
            var right = MathUtilities.RotationToUnitVector(rotation - 0.5f * MathF.PI);

            var originToPosition = position.ToVector2() - p.Value.ToVector2();
            var amountForward = Vector2.Dot(forward, originToPosition);
            var amountRight = Vector2.Dot(right, originToPosition);

            if (amountForward >= 0 &&
                amountRight >= -0.5f * width &&
                amountRight <= 0.5f * width)
            {
                inOmen = true;
                break;
            }
        }

        return inOmen;
    }

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/m0935mist_omen_o0p.avfx"))
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
