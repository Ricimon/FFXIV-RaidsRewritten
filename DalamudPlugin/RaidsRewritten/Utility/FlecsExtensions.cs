using System;
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

    public static bool IsValid<T>(this Query<T> query)
    {
        unsafe
        {
            return query.CPtr() != null && query.Entity().IsValid() && !query.World().ShouldQuit();
        }
    }

    public static bool IsValid<T0, T1>(this Query<T0, T1> query)
    {
        unsafe
        {
            return query.CPtr() != null && query.Entity().IsValid() && !query.World().ShouldQuit();
        }
    }

    public static bool IsValid<T0, T1, T2>(this Query<T0, T1, T2> query)
    {
        unsafe
        {
            return query.CPtr() != null && query.Entity().IsValid() && !query.World().ShouldQuit();
        }
    }

    public static void SafeDispose<T>(this ref Query<T> query)
    {
        if (query.IsValid()) { query.Dispose(); }
    }

    public static void SafeDispose<T0, T1>(this ref Query<T0, T1> query)
    {
        if (query.IsValid()) { query.Dispose(); }
    }

    public static void SafeDispose<T0, T1, T2>(this ref Query<T0, T1, T2> query)
    {
        if (query.IsValid()) { query.Dispose(); }
    }

    public static void SafeDestruct(this Entity entity)
    {
        if (entity.IsValid()) { entity.Destruct(); }
    }

    public static bool HasChildren(this Entity entity)
    {
        var childCount = 0;
        entity.Children(child => { childCount++; });
        return childCount > 0;
    }

    public static bool HasChild<T>(this Entity entity) where T : struct
    {
        var ret = false;
        entity.Children(child =>
        {
            if (child.Has<T>())
            {
                ret = true;
            }
        });
        return ret;
    }

    public static void DestructChildEntity<T>(this Entity entity) where T : struct
    {
        if (!entity.CsWorld().IsDeferred())
        {
            throw new Exception("Destructing entities must be done in a deferred context.");
        }

        entity.Children(child =>
        {
            if (child.Has<T>())
            {
                child.Destruct();
            }
        });
    }
}
