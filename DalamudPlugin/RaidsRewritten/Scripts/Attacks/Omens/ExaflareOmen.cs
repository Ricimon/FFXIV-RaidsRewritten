using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class ExaflareOmen : IAttack
{
    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/yazirushi1o0c.avfx"))
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
