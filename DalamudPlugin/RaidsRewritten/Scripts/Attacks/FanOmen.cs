using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class FanOmen : IAttack
{
    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/gl_fan090_1bf.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Add<Attack>()
            .Add<Omen>();
    }
}
