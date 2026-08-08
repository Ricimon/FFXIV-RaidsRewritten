using Flecs.NET.Core;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Models;

public class Shanoa : IEntity
{
    public const int ModelId = 438;
    public const uint ScratchSelfAnimationId = 4;
    public const uint StretchAnimationId = 5;
    public const uint AttackAnimationId = 6;
    public const uint WalkAnimationId = 7;
    public const uint RunAnimationId = 22;

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(ModelId))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(2.0f));
    }
}
