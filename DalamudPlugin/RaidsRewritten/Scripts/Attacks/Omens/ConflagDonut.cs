using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class ConflagDonut : IEntity
{
    private const float OuterRadius = 40f;
    private const float InnerRadius = 5f;
    public static bool IsInOmen(Entity omen, Vector3 position)
    {
        if (!omen.TryGet<Position>(out var p)) { return false; }
        if (!omen.TryGet<Scale>(out var s)) { return false; }

        var centerV2 = p.Value.ToVector2();
        var positionV2 = position.ToVector2();
        var distance = Vector2.Distance(centerV2, positionV2);

        return InnerRadius <= distance;
    }

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(new Vector3(OuterRadius)))
            .Set(new StaticVfx("vfx/omen/eff/gl_sircle_4005bf.avfx"))
            .Add<Attack>()
            .Add<Omen>();
    }

    public Entity Create(World world) => CreateEntity(world);
}
