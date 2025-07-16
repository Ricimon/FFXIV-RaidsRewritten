using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class RectangleOmen : IAttack
{
    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/general02f.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Add<Attack>()
            .Add<Omen>();
    }
}
