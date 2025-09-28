using Flecs.NET.Core;

namespace RaidsRewritten.Scripts;

public interface IEntity
{
    public Entity Create(World world);
}
