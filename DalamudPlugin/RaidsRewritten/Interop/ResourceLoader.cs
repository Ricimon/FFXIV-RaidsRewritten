using System;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader : IDisposable
{
    public const string StaticVfxCreateSig = "E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08";
    public const string StaticVfxRunSig = "E8 ?? ?? ?? ?? 8B 4B 7C 85 C9";
    public const string StaticVfxRemoveSig = "40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9";

    public const string ActorVfxCreateSig = "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8";
    public const string ActorVfxRemoveSig = "0F 11 48 10 48 8D 05"; // the weird one

    private readonly Lazy<VfxSpawn> vfxSpawn;

    public ResourceLoader(
        Lazy<VfxSpawn> vfxSpawn,
        ISigScanner sigScanner,
        IGameInteropProvider hooks)
    {
        this.vfxSpawn = vfxSpawn;

        hooks.InitializeFromAttributes(this);

        var staticVfxCreateAddress = sigScanner.ScanText(StaticVfxCreateSig);
        var staticVfxRemoveAddress = sigScanner.ScanText(StaticVfxRemoveSig);
        var actorVfxCreateAddress = sigScanner.ScanText(ActorVfxCreateSig);
        var actorVfxRemoveAddressTemp = sigScanner.ScanText(ActorVfxRemoveSig) + 7;
        var actorVfxRemoveAddress = Marshal.ReadIntPtr(actorVfxRemoveAddressTemp + Marshal.ReadInt32(actorVfxRemoveAddressTemp) + 4);

        ActorVfxCreate = Marshal.GetDelegateForFunctionPointer<ActorVfxCreateDelegate>(actorVfxCreateAddress);
        ActorVfxRemove = Marshal.GetDelegateForFunctionPointer<ActorVfxRemoveDelegate>(actorVfxRemoveAddress);
        StaticVfxRemove = Marshal.GetDelegateForFunctionPointer<StaticVfxRemoveDelegate>(staticVfxRemoveAddress);
        StaticVfxRun = Marshal.GetDelegateForFunctionPointer<StaticVfxRunDelegate>(sigScanner.ScanText(StaticVfxRunSig));
        StaticVfxCreate = Marshal.GetDelegateForFunctionPointer<StaticVfxCreateDelegate>(staticVfxCreateAddress);

        StaticVfxCreateHook = hooks.HookFromAddress<StaticVfxCreateDelegate>(staticVfxCreateAddress, StaticVfxNewDetour);
        StaticVfxRemoveHook = hooks.HookFromAddress<StaticVfxRemoveDelegate>(staticVfxRemoveAddress, StaticVfxRemoveDetour);
        ActorVfxCreateHook = hooks.HookFromAddress<ActorVfxCreateDelegate>(actorVfxCreateAddress, ActorVfxNewDetour);
        ActorVfxRemoveHook = hooks.HookFromAddress<ActorVfxRemoveDelegate>(actorVfxRemoveAddress, ActorVfxRemoveDetour);

        StaticVfxCreateHook.Enable();
        StaticVfxRemoveHook.Enable();
        ActorVfxCreateHook.Enable();
        ActorVfxRemoveHook.Enable();
    }

    public void Dispose()
    {
        StaticVfxCreateHook.Dispose();
        StaticVfxRemoveHook.Dispose();
        ActorVfxCreateHook.Dispose();
        ActorVfxRemoveHook.Dispose();
        GC.SuppressFinalize(this);
    }
}
