using Flecs.NET.Core;

namespace RaidsRewritten.Game;

public sealed class EcsContainer
{
    public readonly World World = World.Create();
}
