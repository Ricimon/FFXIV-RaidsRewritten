// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.cs
// 8be61a5
// and https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Constants.cs
// 8be61a5
using System;
using System.Runtime.InteropServices;
using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using RaidsRewritten.Log;

namespace RaidsRewritten.Interop;

public unsafe sealed partial class ResourceLoader : IDisposable
{
    public const string ReadFileSig = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 63 42";
    public const string ReadSqpackSig = "40 56 41 56 48 83 EC ?? 0F BE 02";
    public const string GetResourceSyncSig = "E8 ?? ?? ?? ?? 48 8B C8 8B C3 F0 0F C0 81";
    public const string GetResourceAsyncSig = "E8 ?? ?? ?? 00 48 8B D8 EB ?? F0 FF 83 ?? ?? 00 00";

    public const string StaticVfxRunSig = "E8 ?? ?? ?? ?? B0 02 EB 02";
    public const string StaticVfxRemoveSig = "40 53 48 83 EC 20 48 8B D9 48 8B 89 ?? ?? ?? ?? 48 85 C9 74 28 33 D2 E8 ?? ?? ?? ?? 48 8B 8B ?? ?? ?? ?? 48 85 C9";

    public const string ActorVfxCreateSig = "40 53 55 56 57 48 81 EC ?? ?? ?? ?? 0F 29 B4 24 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 0F B6 AC 24 ?? ?? ?? ?? 0F 28 F3 49 8B F8";
    public const string ActorVfxRemoveSig = "0F 11 48 10 48 8D 05"; // the weird one

    public const string CallTriggerSig = "E8 ?? ?? ?? ?? 0F B7 43 56";

    // https://github.com/Ottermandias/Penumbra.GameData/blob/main/Signatures.cs

    public const string CheckFileStateSig = "E8 ?? ?? ?? ?? 48 85 C0 74 ?? 4C 8B C8 ";

    public const string LoadTexFileLocalSig = "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 49 8B E8 44 88 4C 24";
    public const string LodConfigSig = "48 8B 05 ?? ?? ?? ?? B3";
    public const string TexResourceHandleOnLoadSig = "40 53 55 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B D9";

    public const string LoadMdlFileLocalSig = "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 48 8B 72 ?? 4C 8B EA";
    public const string LoadMdlFileExternSig = "E8 ?? ?? ?? ?? EB 02 B0 F1";

    public const string LoadScdLocalSig = "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 8B 79 ?? 48 8B DA 8B D7";
    public const string SoundOnLoadSig = "40 56 57 41 54 48 81 EC 90 00 00 00 80 3A 0B 45 0F B6 E0 48 8B F2";

    // https://github.com/lmcintyre/Dalamud.FindAnything/blob/a093b2f9e0c20e7d0479c091125ccca5ea09d683/Dalamud.FindAnything/Game/GameWindow.cs#L250

    public const string PlaySoundSig = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? FE C2";

    public const string InitSoundSig = "E8 ?? ?? ?? ?? 8B 5D 77";

    private DalamudServices dalamud;
    private readonly ILogger logger;

    public ResourceLoader(
        DalamudServices dalamud,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.logger = logger;

        var sigScanner = dalamud.SigScanner;
        var hooks = dalamud.GameInteropProvider;

        hooks.InitializeFromAttributes(this);
        EzSignatureHelper.Initialize(this);

        // VFX

        var staticVfxCreateAddress = sigScanner.ScanText(VfxObject.Addresses.Create.String);
        var actorVfxRemoveAddressTemp = sigScanner.ScanText(ActorVfxRemoveSig) + 7;
        var actorVfxRemoveAddress = Marshal.ReadIntPtr(actorVfxRemoveAddressTemp + Marshal.ReadInt32(actorVfxRemoveAddressTemp) + 4);

        ActorVfxRemove = Marshal.GetDelegateForFunctionPointer<ActorVfxRemoveDelegate>(actorVfxRemoveAddress);
        StaticVfxCreate = Marshal.GetDelegateForFunctionPointer<VfxObject.Delegates.Create>(staticVfxCreateAddress);

        StaticVfxCreateHook = hooks.HookFromAddress<VfxObject.Delegates.Create>(staticVfxCreateAddress, StaticVfxNewDetour);
        ActorVfxRemoveHook = hooks.HookFromAddress<ActorVfxRemoveDelegate>(actorVfxRemoveAddress, ActorVfxRemoveDetour);

        ReadSqpackHook.Enable();
        GetResourceSyncHook.Enable();
        GetResourceAsyncHook.Enable();

        StaticVfxCreateHook.Enable();
        StaticVfxRemoveHook.Enable();
        ActorVfxCreateHook.Enable();
        ActorVfxRemoveHook.Enable();

        CheckFileStateHook.Enable();
        LoadMdlFileExternHook.Enable();
        TextureOnLoadHook.Enable();
        SoundOnLoadHook.Enable();

        VfxUseTriggerHook.Enable();
        InitSoundHook.Enable();

        PathResolved += AddCrc;
    }

    public void Dispose()
    {
        PathResolved -= AddCrc;

        ReadSqpackHook.Dispose();
        GetResourceSyncHook.Dispose();
        GetResourceAsyncHook.Dispose();

        StaticVfxCreateHook.Dispose();
        StaticVfxRemoveHook.Dispose();
        ActorVfxCreateHook.Dispose();
        ActorVfxRemoveHook.Dispose();

        CheckFileStateHook.Dispose();
        LoadMdlFileExternHook.Dispose();
        TextureOnLoadHook.Dispose();
        SoundOnLoadHook.Dispose();

        VfxUseTriggerHook.Dispose();
        InitSoundHook.Dispose();
    }
}
