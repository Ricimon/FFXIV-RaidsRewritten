using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class CircleOmen : IAttack
{
    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Vfx("vfx/omen/eff/general_1bf.avfx"))
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
