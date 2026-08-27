// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Vfx.cs
// 8be61a5
using System;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using InteropGenerator.Runtime;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    //====== STATIC ===========
    public VfxObject.Delegates.Create StaticVfxCreate;

    public delegate IntPtr StaticVfxRunDelegate(VfxObject* vfx, float a1, uint a2);

    [Signature(StaticVfxRunSig)]
    public readonly StaticVfxRunDelegate StaticVfxRun = null;

    public delegate IntPtr StaticVfxRemoveDelegate(VfxObject* vfx);

    [Signature(StaticVfxRemoveSig)]
    public readonly StaticVfxRemoveDelegate StaticVfxRemove = null;

    // ======= STATIC HOOKS ========
    public Hook<VfxObject.Delegates.Create> StaticVfxCreateHook { get; private set; }

    [Signature(StaticVfxRemoveSig, DetourName = nameof(StaticVfxRemoveDetour))]
    public readonly Hook<StaticVfxRemoveDelegate> StaticVfxRemoveHook = null;
    public event Action<IntPtr>? OnStaticVfxRemoved;

    // ======== ACTOR =============
    public delegate VfxObject* ActorVfxCreateDelegate(string path, IntPtr a2, IntPtr a3, float a4, char a5, ushort a6, char a7);

    [Signature(ActorVfxCreateSig)]
    public readonly ActorVfxCreateDelegate ActorVfxCreate = null;

    public delegate IntPtr ActorVfxRemoveDelegate(VfxObject* vfx, char a2);

    public ActorVfxRemoveDelegate ActorVfxRemove;

    // ======== ACTOR HOOKS =============
    [Signature(ActorVfxCreateSig, DetourName = nameof(ActorVfxNewDetour))]
    public readonly Hook<ActorVfxCreateDelegate> ActorVfxCreateHook = null;

    public Hook<ActorVfxRemoveDelegate> ActorVfxRemoveHook { get; private set; }
    public event Action<IntPtr, char>? OnActorVfxRemoved;

    // ======= TRIGGERS =============
    public delegate IntPtr VfxUseTriggerDelete(VfxObject* vfx, uint triggerId);

    [Signature(CallTriggerSig, DetourName = nameof(VfxUseTriggerDetour))]
    public readonly Hook<VfxUseTriggerDelete> VfxUseTriggerHook = null;

    // ==============================

    private VfxObject* StaticVfxNewDetour(CStringPointer path, CStringPointer pool)
    {
        var vfx = StaticVfxCreateHook.Original(path, pool);
        //Plugin.TrackerManager?.Vfx.AddStatic(vfx, $"{path}");

        //if (Plugin.Configuration?.LogVfxDebug == true) Dalamud.Log($"New Static: {path} {(nint)vfx:X8}");
        return vfx;
    }

    private IntPtr StaticVfxRemoveDetour(VfxObject* vfx)
    {
        //VfxSpawn.InteropRemoved(vfx);
        //Plugin.TrackerManager?.Vfx.RemoveStatic(vfx);
        OnStaticVfxRemoved?.Invoke((IntPtr)vfx);
        return StaticVfxRemoveHook.Original(vfx);
    }

    private VfxObject* ActorVfxNewDetour(string path, IntPtr a2, IntPtr a3, float a4, char a5, ushort a6, char a7)
    {
        var vfx = ActorVfxCreateHook.Original(path, a2, a3, a4, a5, a6, a7);
        //Plugin.TrackerManager?.Vfx.AddActor(vfx, path);

        //if (Plugin.Configuration?.LogVfxDebug == true) Dalamud.Log($"New Actor: {path} {(nint)vfx:X8}");
        return vfx;
    }

    private IntPtr ActorVfxRemoveDetour(VfxObject* vfx, char a2)
    {
        //VfxSpawn.InteropRemoved(vfx);
        //Plugin.TrackerManager?.Vfx.RemoveActor(vfx);
        OnActorVfxRemoved?.Invoke((IntPtr)vfx, a2);
        return ActorVfxRemoveHook.Original(vfx, a2);
    }

    private IntPtr VfxUseTriggerDetour(VfxObject* vfx, uint triggerId)
    {
        var timeline = VfxUseTriggerHook.Original(vfx, triggerId);

        //if (Plugin.Configuration?.LogVfxTriggers == true) Dalamud.Log($"Trigger {triggerId} on {(nint)vfx:X8}, timeline: {timeline:X8}");
        return timeline;
    }
}
