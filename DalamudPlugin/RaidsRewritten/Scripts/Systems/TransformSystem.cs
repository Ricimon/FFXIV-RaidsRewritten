using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Systems;

public class TransformSystem : ISystem
{
    public void Register(World world)
    {
        world.System<LocalPosition>()
            .Each((Entity e, ref LocalPosition localPosition) =>
            {
                var parentPosition = Vector3.Zero;
                var parent = e.Parent();
                while (parent.IsValid())
                {
                    if (parent.TryGet(out Position p))
                    {
                        parentPosition += p.Value;
                    }
                    parent = parent.Parent();
                }

                e.Set(new Position(parentPosition + localPosition.Value));
            });

        world.System<AngularVelocity, Rotation>()
            .Each((Iter it, int i, ref AngularVelocity angularVelocity, ref Rotation rotation) =>
            {
                rotation.Value += angularVelocity.Value * it.DeltaTime();
            });

        world.System<FullAngularVelocity, FullRotation>()
            .Each((Iter it, int i, ref FullAngularVelocity angularVelocity, ref FullRotation rotation) =>
            {
                // https://math.stackexchange.com/a/39565
                // https://gamedev.stackexchange.com/a/181279
                var d = angularVelocity.Value * it.DeltaTime();
                var m = d.Length();
                if (m == 0)
                {
                    return;
                }
                var v = d / m;
                v *= MathF.Sin(m / 2.0f);

                var deltaRotation = new Quaternion(v.X, v.Y, v.Z, MathF.Cos(m / 2.0f));
                rotation.Value = deltaRotation * rotation.Value;
                rotation.Value = Quaternion.Normalize(rotation.Value);
            });
    }
}
