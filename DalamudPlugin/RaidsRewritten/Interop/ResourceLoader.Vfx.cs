// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Vfx.cs
// ac4aab8
using System;
using Dalamud.Hooking;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    // ======= STATIC =========
    public delegate IntPtr StaticVfxCreateDelegate(string path, string pool);

    public StaticVfxCreateDelegate StaticVfxCreate;

    public delegate IntPtr StaticVfxRunDelegate(IntPtr vfx, float a1, uint a2);

    public StaticVfxRunDelegate StaticVfxRun;

    public delegate IntPtr StaticVfxRemoveDelegate(IntPtr vfx);

    public StaticVfxRemoveDelegate StaticVfxRemove;

    // ======= STATIC HOOKS =========
    public Hook<StaticVfxCreateDelegate> StaticVfxCreateHook { get; private set; }
    
    public Hook<StaticVfxRemoveDelegate> StaticVfxRemoveHook { get; private set; }

    // ======= ACTOR =========
    public delegate IntPtr ActorVfxCreateDelegate(string path, IntPtr a2, IntPtr a3, float a4, char a5, ushort a6, char a7);

    public ActorVfxCreateDelegate ActorVfxCreate;

    public delegate IntPtr ActorVfxRemoveDelegate(IntPtr vfx, char a2);

    public ActorVfxRemoveDelegate ActorVfxRemove;

    // ======= ACTOR HOOKS ========
    public Hook<ActorVfxCreateDelegate> ActorVfxCreateHook { get; private set; }

    public Hook<ActorVfxRemoveDelegate> ActorVfxRemoveHook { get; private set; }

    // ============================

    private IntPtr StaticVfxNewDetour(string path, string pool)
    {
        var vfx = StaticVfxCreateHook.Original(path, pool);
        return vfx;
    }

    private IntPtr StaticVfxRemoveDetour(IntPtr vfx)
    {
        this.vfxSpawn.Value.InteropRemoved(vfx);
        return StaticVfxRemoveHook.Original(vfx);
    }

    private IntPtr ActorVfxNewDetour(string path, IntPtr a2, IntPtr a3, float a4, char a5, ushort a6, char a7)
    {
        var vfx = ActorVfxCreateHook.Original(path, a2, a3, a4, a5, a6, a7);
        return vfx;
    }

    private IntPtr ActorVfxRemoveDetour(IntPtr vfx, char a2)
    {
        this.vfxSpawn.Value.InteropRemoved(vfx);
        return ActorVfxRemoveHook.Original(vfx, a2);
    }
}
