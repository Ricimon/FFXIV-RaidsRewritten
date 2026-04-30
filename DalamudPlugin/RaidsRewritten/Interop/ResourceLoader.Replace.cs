// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Replace.cs
// 9e528e0
using System;
using System.Collections.Generic;
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

    public delegate void* GetResourceSyncPrototype(void* resourceManager, void* category, uint* type,
        uint* hash, byte* path, void* unknown, void* unkDebugPtr, uint unkDebugInt);

    public delegate void* GetResourceAsyncPrototype(void* resourceManager, void* category, uint* type,
        uint* hash, byte* path, void* unknown, bool isUnknown, void* unkDebugPtr, uint unkDebugInt);

    // ===== FILES HOOKS =========

    public Hook<GetResourceSyncPrototype> GetResourceSyncHook { get; private set; }
    public Hook<GetResourceAsyncPrototype> GetResourceAsyncHook { get; private set; }
    public Hook<ReadSqPackPrototype> ReadSqPackHook { get; private set; }
    public ReadFilePrototype ReadFile { get; private set; }

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

    private void* GetResourceSyncDetour(
        void* resourceManager,
        void* category,
        uint* type,
        uint* hash,
        byte* path,
        void* unknown,
        void* unkDebugPtr,
        uint unkDebugInt
    ) => GetResourceHandler(true, resourceManager, category, type, hash, path, unknown, false, unkDebugPtr, unkDebugInt);

    private void* GetResourceAsyncDetour(
        void* resourceManager,
        void* category,
        uint* type,
        uint* hash,
        byte* path,
        void* unknown,
        bool isUnknown,
        void* unkDebugPtr,
        uint unkDebugInt
    ) => GetResourceHandler(false, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);

    private void* CallOriginalHandler(
        bool isSync,
        void* resourceManager,
        void* category,
        uint* type,
        uint* hash,
        byte* path,
        void* unknown,
        bool isUnknown,
        void* unkDebugPtr,
        uint unkDebugInt
    ) => isSync
        ? GetResourceSyncHook.Original(resourceManager, category, type, hash, path, unknown, unkDebugPtr, unkDebugInt)
        : GetResourceAsyncHook.Original(resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);

    private void* GetResourceHandler(
        bool isSync,
        void* resourceManager,
        void* category,
        uint* type,
        uint* hash,
        byte* path,
        void* unknown,
        bool isUnknown,
        void* unkDebugPtr,
        uint unkDebugInt
    )
    {
        if (!Utf8GamePath.FromPointer(path, MetaDataComputation.None, out var gamePath))
        {
            return CallOriginalHandler(isSync, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);
        }

        var gamePathString = gamePath.ToString();

        var replacedPath = GetReplacePath(gamePathString, out var localPath) ? localPath : null;

        if (replacedPath == null || replacedPath.Length >= 260)
        {
            return CallOriginalHandler(isSync, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);
        }

        var resolvedPath = new FullPath(replacedPath);
        PathResolved?.Invoke((ResourceType)(*type), resolvedPath);

        *hash = (uint)InteropUtils.ComputeHash(resolvedPath.InternalName, (GetResourceParameters*)unknown);
        path = resolvedPath.InternalName.Path;

        return CallOriginalHandler(isSync, resourceManager, category, type, hash, path, unknown, isUnknown, unkDebugPtr, unkDebugInt);
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
