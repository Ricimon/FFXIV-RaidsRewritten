// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Replace.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Penumbra.String;
using Penumbra.String.Classes;
using RaidsRewritten.Interop.Structs;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
#nullable enable
    private event Action<ResourceType, FullPath?>? PathResolved;
#nullable disable

    // ===== FILES ========

    public delegate byte ReadFilePrototype(IntPtr fileHandler, SeFileDescriptor* fileDesc, int priority, bool isSync);

    public delegate byte ReadSqPackPrototype(IntPtr fileHandler, SeFileDescriptor* fileDesc, int priority, bool isSync);

    public delegate void* GetResourceSyncPrototype(IntPtr resourceManager, uint* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* resParams);

    public delegate void* GetResourceAsyncPrototype(IntPtr resourceManager, uint* categoryId, ResourceType* resourceType,
        int* resourceHash, byte* path, GetResourceParameters* resParams, bool isUnknown);

    // ===== FILES HOOKS =========

    public Hook<GetResourceSyncPrototype> GetResourceSyncHook { get; private set; }
    public Hook<GetResourceAsyncPrototype> GetResourceAsyncHook { get; private set; }
    public Hook<ReadSqPackPrototype> ReadSqPackHook { get; private set; }
    public ReadFilePrototype ReadFile { get; private set; }

    private bool GetReplacePath(string gamePath, out string? localPath)
    {
        localPath = null;

        if (gamePath == "chara/monster/m7002/obj/body/b0001/model/m7002b0001.mdl")
        {
            //var replacementPath = this.dalamud.PluginInterface.GetResourcePath("m7002b0001.mdl");
            localPath = this.dalamud.PluginInterface.GetResourcePath("nerdy.mdl");
            return true;
        }
        return false;
    }

    private void* GetResourceSyncDetour(
        IntPtr resourceManager,
        uint* categoryId,
        ResourceType* resourceType,
        int* resourceHash,
        byte* path,
        GetResourceParameters* resParams
    ) => GetResourceHandler(true, resourceManager, categoryId, resourceType, resourceHash, path, resParams, false);

    private void* GetResourceAsyncDetour(
        IntPtr resourceManager,
        uint* categoryId,
        ResourceType* resourceType,
        int* resourceHash,
        byte* path,
        GetResourceParameters* resParams,
        bool isUnknown
    ) => GetResourceHandler(false, resourceManager, categoryId, resourceType, resourceHash, path, resParams, isUnknown);

    private void* CallOriginalHandler(
        bool isSync,
        IntPtr resourceManager,
        uint* categoryId,
        ResourceType* resourceType,
        int* resourceHash,
        byte* path,
        GetResourceParameters* resParams,
        bool isUnknown
    ) => isSync
        ? GetResourceSyncHook.Original(resourceManager, categoryId, resourceType, resourceHash, path, resParams)
        : GetResourceAsyncHook.Original(resourceManager, categoryId, resourceType, resourceHash, path, resParams, isUnknown);

    private void* GetResourceHandler(
        bool isSync,
        IntPtr resourceManager,
        uint* categoryId,
        ResourceType* resourceType,
        int* resourceHash,
        byte* path,
        GetResourceParameters* resParams,
        bool isUnknown
    )
    {
        if (!Utf8GamePath.FromPointer(path, MetaDataComputation.None, out var gamePath))
        {
            return CallOriginalHandler(isSync, resourceManager, categoryId, resourceType, resourceHash, path, resParams, isUnknown);
        }

        var gamePathString = gamePath.ToString();

        //if( Plugin.Configuration?.LogAllFiles == true ) {
        //    Dalamud.Log( $"[GetResourceHandler] {gamePathString}" );
        //    if( SelectDialog.LoggedFiles.Count > 1000 ) SelectDialog.LoggedFiles.Clear();
        //    SelectDialog.LoggedFiles.Add( gamePathString );
        //}

        //this.logger.Debug("Processing GetResource Path {0}", gamePathString);

        var replacedPath = GetReplacePath(gamePathString, out var localPath) ? localPath : null;

        if (replacedPath == null || replacedPath.Length >= 260)
        {
            var unreplaced = CallOriginalHandler(isSync, resourceManager, categoryId, resourceType, resourceHash, path, resParams, isUnknown);
            //if( Plugin.Configuration?.LogDebug == true && DoDebug( gamePathString ) ) Dalamud.Log( $"[GetResourceHandler] ORIGINAL: {gamePathString} -> " + new IntPtr( unreplaced ).ToString( "X8" ) );
            return unreplaced;
        }

        //this.logger.Debug("Got Replace Path {0}", replacedPath);

        var resolvedPath = new FullPath(replacedPath);
        PathResolved?.Invoke(*resourceType, resolvedPath);

        *resourceHash = InteropUtils.ComputeHash(resolvedPath.InternalName, resParams);
        path = resolvedPath.InternalName.Path;

        var replaced = CallOriginalHandler(isSync, resourceManager, categoryId, resourceType, resourceHash, path, resParams, isUnknown);
        //if( Plugin.Configuration?.LogDebug == true ) Dalamud.Log( $"[GetResourceHandler] REPLACED: {gamePathString} -> {replacedPath} -> " + new IntPtr( replaced ).ToString( "X8" ) );
        return replaced;
    }

    private byte ReadSqPackDetour(IntPtr fileHandler, SeFileDescriptor* fileDescriptor, int priority, bool isSync)
    {
        if (fileDescriptor == null || fileDescriptor->ResourceHandle == null)
        {
            return this.ReadSqPackHook.Original(fileHandler, fileDescriptor, priority, isSync);
        }

        if (!fileDescriptor->ResourceHandle->GamePath(out var originalGamePath))
        {
            return this.ReadSqPackHook.Original(fileHandler, fileDescriptor, priority, isSync);
        }

        var originalPath = originalGamePath.ToString();
        var isPenumbra = ProcessPenumbraPath(originalPath, out var gameFsPath);
        var isRooted = Path.IsPathRooted(gameFsPath);

        //this.logger.Debug("Processing Sq Path {0}", gameFsPath);
        if (gameFsPath != null && !isRooted)
        {
            var replacementPath = GetReplacePath(gameFsPath, out var localPath) ? localPath : null;
            if (replacementPath != null && Path.IsPathRooted(replacementPath) && replacementPath.Length < 260)
            {
                //this.logger.Debug("Got Replace Path: {0}", replacementPath);
                gameFsPath = replacementPath;
                isRooted = true;
                isPenumbra = false;
            }
        }

        if (gameFsPath == null || gameFsPath.Length >= 260 || !isRooted || isPenumbra)
        {
            return this.ReadSqPackHook.Original(fileHandler, fileDescriptor, priority, isSync);
        }

        //this.logger.Debug($"swap: {originalPath} -> {gameFsPath}");

        fileDescriptor->FileMode = Structs.FileMode.LoadUnpackedResource;

        ByteString.FromString(gameFsPath, out var gamePath);

        // note: must be utf16
        var utfPath = Encoding.Unicode.GetBytes(gameFsPath);
        Marshal.Copy(utfPath, 0, new IntPtr(&fileDescriptor->Utf16FileName), utfPath.Length);
        var fd = stackalloc byte[0x20 + utfPath.Length + 0x16];
        Marshal.Copy(utfPath, 0, new IntPtr(fd + 0x21), utfPath.Length);
        fileDescriptor->FileDescriptor = fd;

        return ReadFile(fileHandler, fileDescriptor, priority, isSync);
    }
}
