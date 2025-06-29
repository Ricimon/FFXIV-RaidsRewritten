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

public unsafe class VfxSpawn : IDisposable
{
    public readonly Dictionary<BaseVfx, VfxSpawnItem> Vfxs = [];
    public readonly List<VfxLoopItem> ToLoop = [];

    private readonly ResourceLoader resourceLoader;
    private readonly ILogger logger;

    public VfxSpawn(ResourceLoader resourceLoader, ILogger logger)
    {
        this.resourceLoader = resourceLoader;
        this.logger = logger;
    }

    public StaticVfx SpawnStaticVfx(string path, Vector3 position, float rotation)
    {
        var vfx = new StaticVfx(this.resourceLoader, path);
        vfx.Create(position, rotation);
        Vfxs.Add(vfx, new(path, SpawnType.Ground, false));
        return vfx;
    }

    public ActorVfx SpawnActorVfx(string path, IGameObject caster, IGameObject target)
    {
        var vfx = new ActorVfx(this.resourceLoader, path);
        vfx.Create(caster, target);
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
        var item = Vfxs[vfx];

        Vfxs.Remove(vfx);
    }

    public bool GetVfx(IntPtr data, out BaseVfx vfx)
    {
        vfx = null;
        if (data == IntPtr.Zero || Vfxs.Count == 0) { return false; }
        return Vfxs.Keys.FindFirst(x => data == (IntPtr)x.Vfx, out vfx);
    }
}
