// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.cs
using System;
using System.Runtime.InteropServices;
using RaidsRewritten.Log;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Interop;

public unsafe sealed partial class ResourceLoader : IDisposable
{
    public const string StaticVfxCreateSig = "E8 ?? ?? ?? ?? F3 0F 10 35 ?? ?? ?? ?? 48 89 43 08";
    public const string StaticVfxRunSig = "E8 ?? ?? ?? ?? B0 02 EB 02";
    public const string StaticVfxRemoveSig = "40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9";

    public const string ActorVfxCreateSig = "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8";
    public const string ActorVfxRemoveSig = "0F 11 48 10 48 8D 05"; // the weird one

    public const string PlaySpecificSoundSig = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F";
    public const string GetResourceSyncSig = "E8 ?? ?? ?? ?? 48 8B D8 8B C7";
    public const string GetResourceAsyncSig = "E8 ?? ?? ?? ?? 48 8B D8 EB 07 F0 FF 83";
    public const string LoadSoundFileSig = "E8 ?? ?? ?? ?? 48 85 C0 75 12 B0 F6";
    public const string ApricotListenerSoundPlaySig = "41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 4D 8B F9";
    public const string ApricotListenerSoundPlayCallerSig = "4C 8B DC 56 48 81 EC ?? ?? ?? ?? F3 0F 10 89";
    public const string PlaySoundSig = "E8 ?? ?? ?? ?? 83 FB 10 41 BF ?? ?? ?? ??";

    private readonly Lazy<VfxSpawn> vfxSpawn;
    private readonly ILogger logger;

    public ResourceLoader(
        DalamudServices dalamud,
        Lazy<VfxSpawn> vfxSpawn,
        ILogger logger)
    {
        this.vfxSpawn = vfxSpawn;
        this.logger = logger;

        var sigScanner = dalamud.SigScanner;
        var hooks = dalamud.GameInteropProvider;

        hooks.InitializeFromAttributes(this);

        // VFX

        var staticVfxCreateAddress = sigScanner.ScanText(StaticVfxCreateSig);
        var staticVfxRemoveAddress = sigScanner.ScanText(StaticVfxRemoveSig);
        var actorVfxCreateAddress = sigScanner.ScanText(ActorVfxCreateSig);
        var actorVfxRemoveAddressTemp = sigScanner.ScanText(ActorVfxRemoveSig) + 7;
        var actorVfxRemoveAddress = Marshal.ReadIntPtr(actorVfxRemoveAddressTemp + Marshal.ReadInt32(actorVfxRemoveAddressTemp) + 4);

        StaticVfxRemove = Marshal.GetDelegateForFunctionPointer<StaticVfxRemoveDelegate>(staticVfxRemoveAddress);
        StaticVfxRun = Marshal.GetDelegateForFunctionPointer<StaticVfxRunDelegate>(sigScanner.ScanText(StaticVfxRunSig));
        StaticVfxCreate = Marshal.GetDelegateForFunctionPointer<StaticVfxCreateDelegate>(staticVfxCreateAddress);
        ActorVfxCreate = Marshal.GetDelegateForFunctionPointer<ActorVfxCreateDelegate>(actorVfxCreateAddress);
        ActorVfxRemove = Marshal.GetDelegateForFunctionPointer<ActorVfxRemoveDelegate>(actorVfxRemoveAddress);

        StaticVfxCreateHook = hooks.HookFromAddress<StaticVfxCreateDelegate>(staticVfxCreateAddress, StaticVfxNewDetour);
        StaticVfxRemoveHook = hooks.HookFromAddress<StaticVfxRemoveDelegate>(staticVfxRemoveAddress, StaticVfxRemoveDetour);
        ActorVfxCreateHook = hooks.HookFromAddress<ActorVfxCreateDelegate>(actorVfxCreateAddress, ActorVfxNewDetour);
        ActorVfxRemoveHook = hooks.HookFromAddress<ActorVfxRemoveDelegate>(actorVfxRemoveAddress, ActorVfxRemoveDetour);

        StaticVfxCreateHook.Enable();
        StaticVfxRemoveHook.Enable();
        ActorVfxCreateHook.Enable();
        ActorVfxRemoveHook.Enable();

        // Sound

        //var playSpecificSoundAddress = sigScanner.ScanText(PlaySpecificSoundSig);
        //var loadSoundFileAddress = sigScanner.ScanText(LoadSoundFileSig);
        //var apricotListenerSoundPlayAddress = sigScanner.ScanText(ApricotListenerSoundPlaySig);

        //PlaySpecificSound = Marshal.GetDelegateForFunctionPointer<PlaySpecificSoundDelegate>(playSpecificSoundAddress);
        //LoadSoundFile = Marshal.GetDelegateForFunctionPointer<LoadSoundFileDelegate>(loadSoundFileAddress);
        //ApricotListenerSoundPlay = Marshal.GetDelegateForFunctionPointer<ApricotListenerSoundPlayDelegate>(apricotListenerSoundPlayAddress);

        //PlaySpecificSoundHook = hooks.HookFromAddress<PlaySpecificSoundDelegate>(playSpecificSoundAddress, PlaySpecificSoundDetour);
        //GetResourceSyncHook = hooks.HookFromSignature<GetResourceSyncPrototype>(GetResourceSyncSig, GetResourceSyncDetour);
        //GetResourceAsyncHook = hooks.HookFromSignature<GetResourceAsyncPrototype>(GetResourceAsyncSig, GetResourceAsyncDetour);
        //LoadSoundFileHook = hooks.HookFromAddress<LoadSoundFileDelegate>(loadSoundFileAddress, LoadSoundFileDetour);
        //ApricotListenerSoundPlayHook = hooks.HookFromAddress<ApricotListenerSoundPlayDelegate>(apricotListenerSoundPlayAddress, ApricotListenerSoundPlayDetour);
        //ApricotListenerSoundPlayCallerHook = hooks.HookFromSignature<ApricotListenerSoundPlayCallerDelegate>(ApricotListenerSoundPlayCallerSig, ApricotListenerSoundPlayCallerDetour);
        //PlaySoundHook = hooks.HookFromSignature<PlaySoundDelegate>(PlaySoundSig, PlaySoundDetour);

        //PlaySpecificSoundHook.Enable();
        //GetResourceSyncHook.Enable();
        //GetResourceAsyncHook.Enable();
        //LoadSoundFileHook.Enable();
        //ApricotListenerSoundPlayHook.Enable();
        //ApricotListenerSoundPlayCallerHook.Enable();
        //PlaySoundHook.Enable();
    }

    public void Dispose()
    {
        StaticVfxCreateHook.Dispose();
        StaticVfxRemoveHook.Dispose();
        ActorVfxCreateHook.Dispose();
        ActorVfxRemoveHook.Dispose();

        //PlaySpecificSoundHook.Dispose();
        //GetResourceSyncHook.Dispose();
        //GetResourceAsyncHook.Dispose();
        //LoadSoundFileHook.Dispose();
        //ApricotListenerSoundPlayHook.Dispose();
        //ApricotListenerSoundPlayCallerHook.Dispose();
        //PlaySoundHook.Dispose();
    }
}
