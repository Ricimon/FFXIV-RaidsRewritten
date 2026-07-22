using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Attacks.Omens;

public class KnockbackOmen : IEntity
{
    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("vfx/omen/eff/nockback_omen03t.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Add<Attack>()
            .Add<Omen>();
    }
}
