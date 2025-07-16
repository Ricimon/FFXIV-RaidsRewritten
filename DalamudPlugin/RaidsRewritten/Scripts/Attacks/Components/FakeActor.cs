using Flecs.NET.Core;

namespace RaidsRewritten.Scripts.Attacks.Components;

public class FakeActor
{
    public record struct Component(object _);

    public static Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(-1))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new Component())
            .Add<Attack>();
    }
}
