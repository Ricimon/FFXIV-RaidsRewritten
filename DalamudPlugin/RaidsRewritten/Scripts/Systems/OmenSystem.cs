using System;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Systems;

public class OmenSystem : ISystem
{
    public void Register(World world)
    {
        world.System<OmenDuration>()
            .Each((Iter it, int i, ref OmenDuration omen) =>
            {
                var entity = it.Entity(i);
                omen.ElapsedTime += it.DeltaTime();

                if (omen.AutoDestruct && omen.ElapsedTime > omen.Duration)
                {
                    entity.Destruct();
                    return;
                }
                else if (omen.Duration - omen.ElapsedTime < 0.25f)
                {
                    if (!entity.Has<FadeOmen>())
                    {
                        entity.Set(new FadeOmen(0.25f));
                    }
                }
            });

        world.System<FadeOmen>()
            .Each((Iter it, int i, ref FadeOmen fade) =>
            {
                var entity = it.Entity(i);
                fade.ElapsedTime += it.DeltaTime();

                var alpha = Math.Clamp(1.0f - (fade.ElapsedTime / fade.Duration), 0, 1);
                entity.Set(new Alpha(alpha));
            });
    }
}
