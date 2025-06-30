using Flecs.NET.Core;

namespace RaidsRewritten.Game;

public interface ISystem
{
    void Register(World world);
}
