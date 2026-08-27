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
    }
}
