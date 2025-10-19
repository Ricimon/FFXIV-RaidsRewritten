// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Spawn/VfxSpawn.cs
// 70661e3
using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using RaidsRewritten.Interop;
using RaidsRewritten.Interop.Structs.Vfx;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Spawn;

public enum SpawnType
{
    None,
    Ground,
    Self,
    Target,
}

public class VfxSpawnItem
{
    public readonly string Path;
    public readonly SpawnType Type;
    public readonly bool CanLoop;

    public VfxSpawnItem(string path, SpawnType type, bool canLoop)
    {
        Path = path;
        Type = type;
        CanLoop = canLoop;
    }
}

public class VfxLoopItem
{
    public VfxSpawnItem Item;
    public DateTime RemovedTime;

    public VfxLoopItem(VfxSpawnItem item, DateTime removedTime)
    {
        Item = item;
        RemovedTime = removedTime;
    }
}

public unsafe sealed class VfxSpawn(ResourceLoader resourceLoader, ILogger logger) : IDisposable
{
    public readonly Dictionary<BaseVfx, VfxSpawnItem> Vfxs = [];
    public readonly List<VfxLoopItem> ToLoop = [];

    public StaticVfx SpawnStaticVfx(string path, Vector3 position, float rotation)
    {
        var vfx = new StaticVfx(resourceLoader, path);
        vfx.Create(position, rotation);
        Vfxs.Add(vfx, new(path, SpawnType.Ground, false));
        return vfx;
    }

    public ActorVfx SpawnActorVfx(string path, IGameObject caster, IGameObject target)
    {
        return SpawnActorVfx(path, caster.Address, target.Address);
    }

    public ActorVfx SpawnActorVfx(string path, nint casterAddress, nint targetAddress)
    {
        var vfx = new ActorVfx(resourceLoader, path);
        vfx.Create(casterAddress, targetAddress);
        Vfxs.Add(vfx, new(path, SpawnType.Target, false));
        return vfx;
    }

    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        foreach(var vfx in Vfxs)
        {
            vfx.Key?.Remove();
        }
        Vfxs.Clear();
        ToLoop.Clear();
    }

    public void InteropRemoved(IntPtr data)
    {
        if (!GetVfx(data, out var vfx)) { return; }

        Vfxs.Remove(vfx);
        // When a VFX pointer is auto removed by this interop (because it finished playing),
        // doing anything else with its pointer value will crash the game.
        if (vfx is ActorVfx actorVfx)
        {
            actorVfx.Vfx = null;
        }
        else if (vfx is StaticVfx staticVfx)
        {
            staticVfx.Vfx = null;
        }
    }

    public bool GetVfx(IntPtr data, out BaseVfx vfx)
    {
        vfx = null!;
        if (data == IntPtr.Zero || Vfxs.Count == 0) { return false; }
        return Vfxs.Keys.FindFirst(x => data == x.GetVfxPointer(), out vfx!);
    }
}
