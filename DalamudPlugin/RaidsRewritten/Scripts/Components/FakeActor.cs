using System.Numerics;
using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Components;

public class FakeActor
{
    public record struct Component(object _);

    /// <summary>
    /// Model is not drawn; most efficient method of attaching an ActorVfx that doesn't require scale/rotation modification
    /// </summary>
    public static Entity CreateInvalidModel(World world)
    {
        return world.Entity()
            .Set(new Model(-1))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(Vector3.One))
            .Set(new UniformScale(1f))
            .Set(new Component())
            .Add<Attack>();
    }

    /// <summary>
    /// Model of ID 0 is drawn with 0 scale
    /// </summary>
    public static Entity CreateZeroScaleModel(World world)
    {
        return world.Entity()
            .Set(new Model(0))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(Vector3.One))
            .Set(new UniformScale(0))
            .Set(new Component())
            .Add<EnsureModelIsDrawn>()
            .Add<Attack>();
    }

    /// <summary>
    /// Model of ID 0 is drawn with 0 alpha
    /// </summary>
    public static Entity CreateZeroAlphaModel(World world)
    {
        return world.Entity()
            .Set(new Model(0))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(Vector3.One))
            .Set(new UniformScale(1f))
            .Set(new Alpha(0))
            .Set(new Component())
            .Add<EnsureModelIsDrawn>()
            .Add<Attack>();
    }
}
