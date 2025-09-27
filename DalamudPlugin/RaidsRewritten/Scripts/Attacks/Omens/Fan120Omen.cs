using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class Fan120Omen: IEntity
{
    private const float Degrees = 120;
    public static bool IsInOmen(Entity omen, Vector3 position)
    {
        if (!omen.TryGet<Position>(out var p)) { return false; }
        if (!omen.TryGet<Rotation>(out var r)) { return false; }
        if (!omen.TryGet<Scale>(out var s)) { return false; }

        var bossPosition = p.Value.ToVector2();
        var playerPosition = position.ToVector2();
        var distanceToBoss = Vector2.Distance(bossPosition, playerPosition);

        var rotationAngle = new Vector2(bossPosition.X + MathF.Sin(r.Value), bossPosition.Y + MathF.Cos(r.Value));
        var angle = MathUtilities.GetAngleBetweenLines(bossPosition, playerPosition, bossPosition, rotationAngle);


        return distanceToBoss < s.Value.Z && (angle <= MathHelper.DegToRad(Degrees / 2) || float.IsNaN(angle));
    }

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/gl_fan120_1bf.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Add<Attack>()
            .Add<Omen>();
    }

    public Entity Create(World world) => CreateEntity(world);
}
