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

    public static bool TryGet<T>(this World world, out T component) where T : struct
    {
        if (world.Has<T>())
        {
            component = world.Get<T>();
            return true;
        }

        component = default;
        return false;
    }

    public static bool IsValid<T>(this Query<T> query)
    {
        unsafe
        {
            return query.CPtr() != null;
        }
    }

    public static bool IsValid<T0, T1>(this Query<T0, T1> query)
    {
        unsafe
        {
            return query.CPtr() != null;
        }
    }

    public static bool IsValid<T0, T1, T2>(this Query<T0, T1, T2> query)
    {
        unsafe
        {
            return query.CPtr() != null;
        }
    }

    public static bool HasChildren(this Entity entity)
    {
        var childCount = 0;
        entity.Children(child => { childCount++; });
        return childCount > 0;
    }
}
