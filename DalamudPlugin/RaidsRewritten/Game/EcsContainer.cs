using Flecs.NET.Core;

namespace RaidsRewritten.Game;

public sealed class EcsContainer
{
    public World Stage => World.GetStage(0);

    public readonly World World = World.Create();
}
