// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Replace.cs
// 8be61a5
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.File;
using FFXIVClientStructs.FFXIV.Client.System.Resource;
using InteropGenerator.Runtime;
using Penumbra.String;
using Penumbra.String.Classes;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Utility;
using FileMode = FFXIVClientStructs.FFXIV.Client.System.File.FileMode;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
#nullable enable
    private event Action<ResourceType, FullPath?>? PathResolved;
#nullable disable

    // ===== FILES ========

    public delegate byte ReadFilePrototype(IntPtr fileHandler, FileDescriptor* fileDesc, int priority, bool isSync);

    public delegate byte ReadSqpackPrototype(IntPtr fileHandler, FileDescriptor* fileDesc, int priority, bool isSync);

    public delegate ResourceHandle* GetResourceSyncPrototype(ResourceManager* resourceManager, ResourceCategory* category, uint* type, uint* hash, CStringPointer path,
        void* unknown, void* unkDebugPtr, uint unkDebugInt);

    public delegate ResourceHandle* GetResourceAsyncPrototype(ResourceManager* resourceManager, ResourceCategory* category, uint* type, uint* hash, CStringPointer path,
        void* unknown, bool isUnknown, void* unkDebugPtr, uint unkDebugInt);

    // ===== FILES HOOKS =========

    [Signature(GetResourceSyncSig, DetourName = nameof(GetResourceSyncDetour))]
    public readonly Hook<GetResourceSyncPrototype> GetResourceSyncHook = null;
    [Signature(GetResourceAsyncSig, DetourName = nameof(GetResourceAsyncDetour))]
    public readonly Hook<GetResourceAsyncPrototype> GetResourceAsyncHook = null;
    [Signature(ReadSqpackSig, DetourName = nameof(ReadSqpackDetour))]
    public Hook<ReadSqpackPrototype> ReadSqpackHook = null;
    [Signature(ReadFileSig)]
    public ReadFilePrototype ReadFile = null;

    private ResourceHandle* GetResourceSyncDetour(
        ResourceManager* resourceManager, ResourceCategory* category, uint* type, uint* hash, CStringPointer path,
        void* unknown, void* unkDebugPtr, uint unkDebugInt
    ) => GetResourceHandler(true, resourceManager, category, type, hash, path, unknown, false, unkDebugPtr, unkDebugInt);

    private ResourceHandle* GetResourceAsyncDetour(
        ResourceManager* resourceManager, ResourceCategory* category, uint* type, uint* hash, CStringPointer path,
        void* unknown, bool isUnknown, void* unkDebugPtr, uint unkDebugInt
    ) => GetResourceHandler(false, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);

    private ResourceHandle* CallOriginalHandler(
        bool isSync,
        ResourceManager* resourceManager, ResourceCategory* category, uint* type, uint* hash, CStringPointer path,
        void* unknown, bool isUnknown, void* unkDebugPtr, uint unkDebugInt
    ) => isSync
        ? GetResourceSyncHook.Original(resourceManager, category, type, hash, path, unknown, unkDebugPtr, unkDebugInt)
        : GetResourceAsyncHook.Original(resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);

    private ResourceHandle* GetResourceHandler(
        bool isSync,
        ResourceManager* resourceManager, ResourceCategory* category, uint* type, uint* hash, CStringPointer path,
        void* unknown, bool isUnknown, void* unkDebugPtr, uint unkDebugInt
    )
    {
        if (!Utf8GamePath.FromPointer(path, MetaDataComputation.None, out var gamePath))
        {
            return CallOriginalHandler(isSync, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);
        }

        var gamePathString = gamePath.ToString();

        //if( Plugin.Configuration?.LogAllFiles == true ) {
        //    Dalamud.Log( $"[GetResourceHandler] {gamePathString}" );
        //    if( SelectDialog.LoggedFiles.Count > 1000 ) SelectDialog.LoggedFiles.Clear();
        //    SelectDialog.LoggedFiles.Add( gamePathString );
        //}

        var replacedPath = GetReplacePath(gamePathString, out var localPath) ? localPath : null;

        if (replacedPath == null || replacedPath.Length >= 260)
        {
            var unreplaced = CallOriginalHandler(isSync, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);
            //if( Plugin.Configuration?.LogDebug == true && DoDebug( gamePathString ) ) Dalamud.Log( $"[GetResourceHandler] ORIGINAL: {gamePathString} -> " + new IntPtr( unreplaced ).ToString( "X8" ) );
            return unreplaced;
        }

        var resolvedPath = new FullPath(replacedPath);
        PathResolved?.Invoke((ResourceType)(*type), resolvedPath);

        *hash = (uint)InteropUtils.ComputeHash(resolvedPath.InternalName, (GetResourceParameters*)unknown);
        path = resolvedPath.InternalName.Path;

        var replaced = CallOriginalHandler(isSync, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);
        //if( Plugin.Configuration?.LogDebug == true ) Dalamud.Log( $"[GetResourceHandler] REPLACED: {gamePathString} -> {replacedPath} -> " + new IntPtr( replaced ).ToString( "X8" ) );
        return replaced;
    }

    private byte ReadSqpackDetour(IntPtr fileHandler, FileDescriptor* fileDesc, int priority, bool isSync)
    {
        if (fileDesc->ResourceHandle == null) return ReadSqpackHook.Original(fileHandler, fileDesc, priority, isSync);

        if (!Utf8GamePath.FromSpan(fileDesc->ResourceHandle->FileName.AsSpan(), MetaDataComputation.All, out var originalGamePath))
        {
            return ReadSqpackHook.Original(fileHandler, fileDesc, priority, isSync);
        }

        var originalPath = originalGamePath.ToString();
        var isPenumbra = ProcessPenumbraPath(originalPath, out var actualPath);

        //if( Plugin.Configuration?.LogDebug == true ) Dalamud.Log( $"[ReadSqpackHandler] {actualPath}" );

        var isRooted = Path.IsPathRooted(actualPath);

        // looking for refreshed paths, could also be like |default_1|path.avfx
        if (actualPath != null && !isRooted)
        {
            var replacementPath = GetReplacePath(actualPath, out var localPath) ? localPath : null;
            if (replacementPath != null && Path.IsPathRooted(replacementPath) && replacementPath.Length < 260)
            {
                actualPath = replacementPath;
                isRooted = true;
                isPenumbra = false;
            }
        }

        // call the original if it's a penumbra path that doesn't need replacement as well
        if (actualPath == null || actualPath.Length >= 260 || !isRooted || isPenumbra)
        {
            //if( Plugin.Configuration?.LogDebug == true ) Dalamud.Log( $"[ReadSqpackHandler] ORIGINAL: {originalPath}" );
            return ReadSqpackHook.Original(fileHandler, fileDesc, priority, isSync);
        }

        //if( Plugin.Configuration?.LogDebug == true ) Dalamud.Log( $"[ReadSqpackHandler] REPLACED: {actualPath}" );

        fileDesc->FileMode = FileMode.LoadUnpackedResource;
        ByteString.FromString(actualPath, out var gamePath);

        // note: must be utf16
        var utfPath = Encoding.Unicode.GetBytes(actualPath);
        Marshal.Copy(utfPath, 0, (nint)fileDesc + 0x70, utfPath.Length);
        var fi = stackalloc byte[0x20 + utfPath.Length + 0x16];
        Marshal.Copy(utfPath, 0, (nint)fi + 0x21, utfPath.Length);
        fileDesc->FileInterface = (FileInterface*)fi;

        return ReadFile(fileHandler, fileDesc, priority, isSync);
    }

    //
    // Local file replacement data and methods for bootstrapping adapted code
    //
    public IReadOnlyDictionary<string, string> FileReplacements => fileReplacements;

    private readonly Dictionary<string, string> fileReplacements = [];

    public void AddFileReplacement(string originalPath, string replacementPath)
    {
        this.fileReplacements[originalPath] = replacementPath;
    }

    public void RemoveFileReplacement(string originalPath)
    {
        this.fileReplacements.Remove(originalPath);
    }

    private bool GetReplacePath(string gamePath, out string localPath)
    {
        return FileReplacements.TryGetValue(gamePath, out localPath);
    }
}
