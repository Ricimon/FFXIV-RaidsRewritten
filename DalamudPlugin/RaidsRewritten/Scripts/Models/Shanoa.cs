using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Models;

public class Shanoa(ILogger logger) : IEntity, ISystem
{
    public const int ModelId = 438;
    public const ushort ScratchSelfAnimationId = 4;
    public const ushort StretchAnimationId = 5;
    public const ushort AttackAnimationId = 6;
    public const ushort WalkAnimationId = 7;
    public const ushort RunAnimationId = 22;

    public record struct Component(float MovementSpeed, float RotationSpeed);
    public record struct TargetPosition(Vector3 Value);
    public record struct TargetRotation(float Value);

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Model(ModelId))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new UniformScale(2.0f))
            .Set(new TimelineBase(0))
            .Set(new Component());
    }

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, TimelineBase>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref TimelineBase timeline) =>
            {
                var entity = it.Entity(i);

                var moving = false;

                if (entity.TryGet(out TargetPosition targetPosition))
                {
                    moving = true;

                    var distance = Vector3.Distance(position.Value, targetPosition.Value);
                    var canMoveDistance = component.MovementSpeed * it.DeltaTime();
                    if (distance <= canMoveDistance)
                    {
                        position.Value = targetPosition.Value;
                        entity.Remove<TargetPosition>();
                    }
                    else
                    {
                        var toTarget = Vector3.Normalize(targetPosition.Value - position.Value);
                        position.Value += canMoveDistance * toTarget;
                    }
                }

                if (entity.TryGet(out TargetRotation targetRotation))
                {
                    moving = true;

                    var rotationNeeded = MathUtilities.GetShortestRotationDirection(rotation.Value, targetRotation.Value);
                    var canRotateAngle = component.RotationSpeed * it.DeltaTime();
                    //logger.Info("rotation needed:{0}pi, rotation: {1}pi, targetRotation:{2}pi", rotationNeeded / MathF.PI, rotation.Value / MathF.PI, targetRotation.Value / MathF.PI);
                    if (MathF.Abs(rotationNeeded) <= canRotateAngle)
                    {
                        rotation.Value = MathUtilities.ClampRadians(targetRotation.Value);
                        entity.Remove<TargetRotation>();
                    }
                    else
                    {
                        rotation.Value += MathF.Sign(rotationNeeded) * canRotateAngle;
                        rotation.Value = MathUtilities.ClampRadians(rotation.Value);
                    }
                }

                if (moving)
                {
                    if (timeline.Value != RunAnimationId)
                    {
                        timeline.Value = RunAnimationId;
                    }
                }
                else
                {
                    timeline.Value = 0;
                }
            });
    }
}
