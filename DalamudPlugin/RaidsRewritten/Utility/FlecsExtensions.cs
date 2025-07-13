using Flecs.NET.Core;

namespace RaidsRewritten.Utility;

public static class FlecsExtensions
{
    public static bool TryGet<T>(this Entity entity, out T component) where T : struct
    {
        if (entity.Has<T>())
        {
            component = entity.Get<T>();
            return true;
        }

        component = default;
        return false;
    }
}
