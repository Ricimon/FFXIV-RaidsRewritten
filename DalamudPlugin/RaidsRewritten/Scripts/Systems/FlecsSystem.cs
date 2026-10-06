using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Systems;

public class FlecsSystem : ISystem
{
    public void Register(World world)
    {
        world.System<LinkedEntity>()
            .Each((Entity e, ref LinkedEntity linkedEntity) =>
            {
                if (!linkedEntity.Entity.IsValid())
                {
                    e.Destruct();
                }
            });

        world.System()
            .With<DestructIfNoChildren>()
            .Each((Entity e) =>
            {
                if (!e.HasChildren())
                {
                    e.Destruct();
                }
            });
    }
}
