﻿using Flecs.NET.Core;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class RectangleOmen : IAttack
{
    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Vfx("vfx/omen/eff/general02f.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Add<Attack>()
            .Add<Omen>();
    }
}
