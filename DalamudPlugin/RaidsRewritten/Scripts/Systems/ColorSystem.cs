using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Systems;

public class ColorSystem : ISystem
{
    public void Register(World world)
    {
        world.System<Color>()
            .With<Alpha>()
            .Each((Iter it, int i, ref Color color) =>
            {
                if (it.Changed())
                {
                    var e = it.Entity(i);
                    if (e.TryGet(out Alpha alpha) && alpha.Value != color.Value.W)
                    {
                        it.Entity(i).Set(new Alpha(color.Value.W));
                    }
                }
            });

        world.System<Alpha>()
            .With<Color>()
            .Each((Iter it, int i, ref Alpha alpha) =>
            {
                if (it.Changed())
                {
                    var e = it.Entity(i);
                    if (e.TryGet(out Color color) && color.Value.W != alpha.Value)
                    {
                        e.Set(new Color(color.Value.WithAlpha(alpha.Value)));
                    }
                }
            });
    }
}
