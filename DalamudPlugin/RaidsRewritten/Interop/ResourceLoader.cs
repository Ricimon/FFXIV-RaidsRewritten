// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.cs
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Log;
using RaidsRewritten.Spawn;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Unicode;
using static Flecs.NET.Core.Ecs.Units;

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

    private delegate byte ReadSqPackDelegate(void* resourceManager, SeFileDescriptor* pFileDesc, int priority, bool isSync);

    [Signature("40 56 41 56 48 83 EC 28 0F BE 02", DetourName = nameof(ReadSqPackDetour))]
    private Hook<ReadSqPackDelegate> ReadSqPackHook;

    [Signature("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 54 41 55 41 56 41 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 63 42")]
    private delegate* unmanaged<void*, SeFileDescriptor*, int, bool, byte> _readFile;

    private const string ModName = "m7002b0001.mdl";
    private const string FolderName = "RaidsRewritten";
    private string ConfigPath = "";

    private readonly Lazy<VfxSpawn> vfxSpawn;
    private readonly ILogger logger;
    private DalamudServices dalamud;

    public ResourceLoader(
        DalamudServices dalamud,
        Lazy<VfxSpawn> vfxSpawn,
        ILogger logger)
    {
        this.dalamud = dalamud;
        this.vfxSpawn = vfxSpawn;
        this.logger = logger;
        this.ConfigPath = dalamud.PluginInterface.GetPluginConfigDirectory();
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
        LoadMdlFileLocal = Marshal.GetDelegateForFunctionPointer<LoadMdlFileLocalDelegate>(sigScanner.ScanText(LoadMdlFileLocalSig));

        StaticVfxCreateHook = hooks.HookFromAddress<StaticVfxCreateDelegate>(staticVfxCreateAddress, StaticVfxNewDetour);
        StaticVfxRemoveHook = hooks.HookFromAddress<StaticVfxRemoveDelegate>(staticVfxRemoveAddress, StaticVfxRemoveDetour);
        ActorVfxCreateHook = hooks.HookFromAddress<ActorVfxCreateDelegate>(actorVfxCreateAddress, ActorVfxNewDetour);
        ActorVfxRemoveHook = hooks.HookFromAddress<ActorVfxRemoveDelegate>(actorVfxRemoveAddress, ActorVfxRemoveDetour);
        LoadMdlFileExternHook = hooks.HookFromSignature<LoadMdlFileExternDelegate>(LoadMdlFileExternSig, LoadMdlFileExternDetour);

        StaticVfxCreateHook.Enable();
        StaticVfxRemoveHook.Enable();
        ActorVfxCreateHook.Enable();
        ActorVfxRemoveHook.Enable();
        ReadSqPackHook!.Enable();
        LoadMdlFileExternHook.Enable();

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
        ReadSqPackHook.Dispose();
        LoadMdlFileExternHook.Dispose();

        //PlaySpecificSoundHook.Dispose();
        //GetResourceSyncHook.Dispose();
        //GetResourceAsyncHook.Dispose();
        //LoadSoundFileHook.Dispose();
        //ApricotListenerSoundPlayHook.Dispose();
        //ApricotListenerSoundPlayCallerHook.Dispose();
        //PlaySoundHook.Dispose();
    }

    private byte ReadSqPackDetour(void* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        try
        {
            return this.ReadSqPackDetourInner(resourceManager, fileDescriptor, priority, isSync);
        } catch (Exception ex)
        {
            dalamud.Log.Error(ex, "Error in ReadSqPackDetour");
            return this.ReadSqPackHook.Original(resourceManager, fileDescriptor, priority, isSync);
        }
    }

    private byte ReadSqPackDetourInner(void* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        dalamud.Log.Debug("hi");
        if (fileDescriptor == null || fileDescriptor->ResourceHandle == null)
        {
            goto Original;
        }

        var fileName = fileDescriptor->ResourceHandle->FileName;
        if (fileName.BasicString.First == null)
        {
            goto Original;
        }

        var path = fileName.ToString();
        if (fileName.ToString() != "chara/monster/m7002/obj/body/b0001/model/m7002b0001.mdl")
        {
            goto Original;
        }

        var newPath = Path.Join(ConfigPath, FolderName, ModName);
        dalamud.Log.Debug($"swap: {fileName} -> {newPath}");
        if (!Path.IsPathRooted(newPath)) { goto Original; }
        return this.DefaultRootedResourceLoad(newPath, resourceManager, fileDescriptor, priority, isSync);

    Original:
        return this.ReadSqPackHook.Original(resourceManager, fileDescriptor, priority, isSync);
    }

    // Load the resource from a path on the users hard drives.
    private byte DefaultRootedResourceLoad(string gamePath, void* resourceManager, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        // Specify that we are loading unpacked files from the drive.
        // We need to obtain the actual file path in UTF16 (Windows-Unicode) on two locations,
        // but we write a pointer to the given string instead and use the CreateFileW hook to handle it,
        // because otherwise we are limited to 260 characters.
        fileDescriptor->FileMode = FFXIVClientStructs.FFXIV.Client.System.File.FileMode.LoadUnpackedResource;
        var utf8string = Encoding.Default.GetBytes(gamePath);
        fixed (byte* utf8stringptr = utf8string)
        {
            // Ensure that the file descriptor has its wchar_t array on aligned boundary even if it has to be odd.
            var fd = stackalloc char[0x11 + 0x0B + 14];
            fileDescriptor->FileDescriptor = (byte*)fd + 1;
            WritePtr(fd + 0x11, utf8stringptr, gamePath.Length);
            WritePtr(&fileDescriptor->Utf16FileName, utf8stringptr, gamePath.Length);
        }

        // Use the SE ReadFile function.
        var ret = this._readFile(resourceManager, fileDescriptor, priority, isSync);
        return ret;
    }

    // from penumbra
    public const int Size = 28;
    private const char Prefix = (char)((byte)'P' | (('?' & 0x00FF) << 8));
    public static void WritePtr(char* buffer, byte* address, int length)
    {
        // Set the prefix, which is not valid for any actual path.
        buffer[0] = Prefix;

        var ptr = (byte*)buffer;
        var v = (ulong)address;
        var l = (uint)length;

        // Since the game calls wstrcpy without a length, we need to ensure
        // that there is no wchar_t (i.e. 2 bytes) of 0-values before the end.
        // Fill everything with 0xFF and use every second byte.
        var basePtr = ptr + 2;
        for (int i = 0; i < 23; i++)
        {
            basePtr[i] = 0xFF;
        }
        //MemoryUtility.MemSet(ptr + 2, 0xFF, 23);

        // Write the byte pointer.
        ptr[2] = (byte)(v >> 0);
        ptr[4] = (byte)(v >> 8);
        ptr[6] = (byte)(v >> 16);
        ptr[8] = (byte)(v >> 24);
        ptr[10] = (byte)(v >> 32);
        ptr[12] = (byte)(v >> 40);
        ptr[14] = (byte)(v >> 48);
        ptr[16] = (byte)(v >> 56);

        // Write the length.
        ptr[18] = (byte)(l >> 0);
        ptr[20] = (byte)(l >> 8);
        ptr[22] = (byte)(l >> 16);
        ptr[24] = (byte)(l >> 24);

        ptr[Size - 2] = 0;
        ptr[Size - 1] = 0;
    }


    // some other stuff penumbra/vfxeditor did
    public const string LoadMdlFileLocalSig = "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 48 8B 72 ?? 4C 8B EA";
    public const string LoadMdlFileExternSig = "E8 ?? ?? ?? ?? EB 02 B0 F1";

    public delegate byte LoadMdlFileLocalDelegate(ResourceHandle* handle, IntPtr unk1, bool unk2);

    public LoadMdlFileLocalDelegate LoadMdlFileLocal { get; private set; }

    public delegate byte LoadMdlFileExternDelegate(ResourceHandle* handle, IntPtr unk1, bool unk2, IntPtr unk3);

    public Hook<LoadMdlFileExternDelegate> LoadMdlFileExternHook { get; private set; }

    public static readonly IntPtr CustomFileFlag = new(0xDEADBEEE);
    private byte LoadMdlFileExternDetour(ResourceHandle* resourceHandle, IntPtr unk1, bool unk2, IntPtr ptr)
    {
        var somebool = ptr.Equals(CustomFileFlag);
        if (somebool)
        {
            dalamud.Log.Debug("asdf");
            return LoadMdlFileLocal.Invoke(resourceHandle, unk1, unk2);
        } else
        {
            dalamud.Log.Debug("qwerty");
            return LoadMdlFileExternHook.Original(resourceHandle, unk1, unk2, ptr);
        }
    }
}
