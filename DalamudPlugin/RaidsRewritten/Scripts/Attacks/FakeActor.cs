using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Spawn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

internal class FakeActor(DalamudServices dalamud, VfxSpawn vfxSpawn, Random random, ILogger logger) : IAttack
{
    public record struct Component(object _);
    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(0))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale(Vector3.One))
            .Set(new UniformScale(1f))
            .Set(new Alpha(0f))
            .Set(new Component())
            .Add<Attack>();
    }
}
