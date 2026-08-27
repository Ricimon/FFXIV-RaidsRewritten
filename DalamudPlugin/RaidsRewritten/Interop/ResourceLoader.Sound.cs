// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Sound.cs
// 8be61a5
using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Sound;
using InteropGenerator.Runtime;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    private IntPtr OverridenSound = IntPtr.Zero;
    private int OverridenSoundIdx = -1;

    // ====== PLAY SOUND =======

    public delegate IntPtr PlaySoundDelegate(IntPtr path, byte play);

    [Signature(PlaySoundSig)]
    public readonly PlaySoundDelegate PlaySoundPath = null;

    public void PlaySound(string path, int idx)
    {
        //if (string.IsNullOrEmpty(path) || idx < 0 || !Plugin.ScdManager.FileExists(path)) return;
        if (string.IsNullOrEmpty(path) || idx < 0 || !FileExists(path)) return;

        var bytes = Encoding.ASCII.GetBytes(path);
        var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr + bytes.Length, 0);

        OverridenSound = ptr;
        OverridenSoundIdx = idx;

        PlaySoundPath(ptr, 1);

        OverridenSound = IntPtr.Zero;
        OverridenSoundIdx = -1;

        Marshal.FreeHGlobal(ptr);
    }

    // ====== INIT SOUND =========

    public delegate SoundData* InitSoundPrototype(SoundManager* manager, CStringPointer path, float volume, uint soundIdx, uint unk1, bool unk2, SoundVolumeCategory category);

    [Signature(InitSoundSig, DetourName = nameof(InitSoundDetour))]
    public readonly Hook<InitSoundPrototype> InitSoundHook = null;

    private SoundData* InitSoundDetour(SoundManager* manager, CStringPointer path, float volume, uint soundIdx, uint unk1, bool unk2, SoundVolumeCategory category)
    {
        if (path.HasValue && (nint)path.Value == OverridenSound)
        {
            return InitSoundHook.Original(manager, path, volume, (uint)OverridenSoundIdx, unk1, unk2, category);
        }

        return InitSoundHook.Original(manager, path, volume, soundIdx, unk1, unk2, category);
    }

    private bool FileExists(string path)
    {
        try
        {
            return dalamud.DataManager.FileExists(path);
        }
        catch (Exception) { return false; }
    }
}
