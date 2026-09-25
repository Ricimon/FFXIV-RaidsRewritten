using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Models;

public class SteamChakram(ILogger logger) : IEntity, ISystem
{
    public record struct EntryAnimation(float TimeElapsed = 0.0f, Vector3 StartingPosition = default);
    public record struct AttackAnimation(float TimeElapsed = 0.0f, Vector3 StartingPosition = default);

    private readonly List<(float, float)> entryKeyframes =
    [
        (0.0f, 5.0f),
        (1.0f, 0.0f),
    ];
    private readonly List<(float, float)> attackKeyframes =
    [
        (0.5f, 0.0f),
        (1.0f, 50.0f),
    ];

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(1425))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(1.5f))
            .Set(new TimelineBase(0))
            .Set(new EntryAnimation())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Position, EntryAnimation>()
            .Each((Iter it, int i, ref Position position, ref EntryAnimation animation) =>
            {
                var entity = it.Entity(i);

                if (animation.TimeElapsed == 0)
                {
                    it.World().Entity()
                        .Set(new ActorVfx("vfx/monster/m0315/eff/m0315sp_pop0t.avfx"))
                        .ChildOf(entity);

                    animation.StartingPosition = position.Value;
                }

                animation.TimeElapsed += it.DeltaTime();

                var lastKeyframe = entryKeyframes[^1];
                if (animation.TimeElapsed >= lastKeyframe.Item1)
                {
                    position.Value = animation.StartingPosition + lastKeyframe.Item2 * Vector3.UnitY;
                    entity.Remove<EntryAnimation>();
                    return;
                }

                var k1 = (0.0f, 0.0f);
                var k2 = (0.0f, 0.0f);
                for (var j = entryKeyframes.Count - 1; j >= 1; j--)
                {
                    var k = entryKeyframes[j];
                    if (animation.TimeElapsed < k.Item1)
                    {
                        k2 = k;
                        k1 = entryKeyframes[j - 1];
                    }
                    else
                    {
                        break;
                    }
                }

                var t = MathUtilities.InverseLerp(k1.Item1, k2.Item1, animation.TimeElapsed);
                position.Value = animation.StartingPosition + MathUtilities.Tween(k1.Item2, k2.Item2, t, MathUtilities.Ease.EaseOutBack) * Vector3.UnitY;
                //logger.Info(position.Value.ToString());
            });

        world.System<Position, Rotation, AttackAnimation>()
            .Each((Iter it, int i, ref Position position, ref Rotation rotation, ref AttackAnimation animation) =>
            {
                var entity = it.Entity(i);

                if (animation.TimeElapsed == 0)
                {
                    entity.Set(new TimelineBase(4577));

                    it.World().Entity()
                        .Set(new ActorVfx("vfx/monster/m0315/eff/m0315hsp01c0t.avfx"))
                        .ChildOf(entity);

                    animation.StartingPosition = position.Value;
                }

                animation.TimeElapsed += it.DeltaTime();

                var lastKeyframe = attackKeyframes[^1];
                if (animation.TimeElapsed >= lastKeyframe.Item1)
                {
                    position.Value = animation.StartingPosition + lastKeyframe.Item2 * MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(position.Value.Y);
                    entity.Destruct();
                    return;
                }

                var k1 = (0.0f, 0.0f);
                var k2 = (0.0f, 0.0f);
                for (var j = attackKeyframes.Count - 1; j >= 1; j--)
                {
                    var k = attackKeyframes[j];
                    if (animation.TimeElapsed < k.Item1)
                    {
                        k2 = k;
                        k1 = attackKeyframes[j - 1];
                    }
                    else
                    {
                        break;
                    }
                }

                var t = MathUtilities.InverseLerp(k1.Item1, k2.Item1, animation.TimeElapsed);
                position.Value = animation.StartingPosition + MathUtilities.Tween(k1.Item2, k2.Item2, t, MathUtilities.Ease.EaseInSine) * MathUtilities.RotationToUnitVector(rotation.Value).ToVector3(position.Value.Y);
                //logger.Info(position.Value.ToString());
            });
    }
}
