// Adapted from https://git.anna.lgbt/anna/SoundFilter/src/branch/main/SoundFilter/Filter.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    #region Delegates

    public delegate void* PlaySpecificSoundDelegate(long a1, int idx);

    public delegate IntPtr LoadSoundFileDelegate(IntPtr resourceHandle, uint a2);

    public delegate IntPtr ApricotListenerSoundPlayDelegate(IntPtr a1, int a2, int a3, int* a4, long* a5, long* a6);

    public delegate IntPtr ApricotListenerSoundPlayCallerDelegate(nint a1, nint a2, float a3);

    public delegate IntPtr PlaySoundDelegate(IntPtr a1, IntPtr a2, float a3, int a4, int a5, int a6, int a7, int a8, int a9, int a10, byte a11, uint a12, char a13, int a14, char a15, char a16, char a17, char a18);

    #endregion

    #region Hooks

    public Hook<PlaySpecificSoundDelegate> PlaySpecificSoundHook { get; private set; }

    public Hook<LoadSoundFileDelegate> LoadSoundFileHook { get; private set; }

    public Hook<ApricotListenerSoundPlayDelegate> ApricotListenerSoundPlayHook { get; private set; }

    public Hook<ApricotListenerSoundPlayCallerDelegate> ApricotListenerSoundPlayCallerHook { get; private set; }

    public Hook<PlaySoundDelegate> PlaySoundHook { get; private set; }

    #endregion

    public PlaySpecificSoundDelegate PlaySpecificSound;

    public LoadSoundFileDelegate LoadSoundFile;

    public ApricotListenerSoundPlayDelegate ApricotListenerSoundPlay;

    private const int ResourceDataPointerOffset = 0xB0;

    //private ConcurrentDictionary<IntPtr, string> Scds { get; } = [];

    private void* PlaySpecificSoundDetour(long a1, int idx)
    {
        var scdData = *(byte**)(a1 + 8);
        this.logger.Debug($"PlaySpecificSound scd:0x{(IntPtr)scdData:X}, a1:0x{a1:X}, idx:{idx}");
        return PlaySpecificSoundHook.Original(a1, idx);
    }

    private IntPtr LoadSoundFileDetour(IntPtr resourceHandle, uint a2)
    {
        var ret = LoadSoundFileHook.Original(resourceHandle, a2);

        try
        {
            var handle = (ResourceHandle*)resourceHandle;
            var name = handle->FileName.ToString();
            if (name.EndsWith(".scd"))
            {
                var scdData = Marshal.ReadIntPtr(resourceHandle + ResourceDataPointerOffset);
                this.logger.Debug($"LoadSoundFile {name}, scd:0x{scdData:X}");
            }
        }
        catch (Exception ex)
        {
            this.logger.Error(ex.ToStringFull());
        }
        return ret;
    }

    private IntPtr ApricotListenerSoundPlayDetour(IntPtr a1, int a2, int a3, int* a4, long* a5, long* a6)
    {
        this.logger.Debug($"ApricotListenerSoundPlay a1:0x{a1:X}, a2:{a2}, a3:{a3}, a4:{*a4}, a5:{*a5}, a6:{*a6}");
        return ApricotListenerSoundPlayHook.Original(a1, a2, a3, a4, a5, a6);
    }

    private IntPtr ApricotListenerSoundPlayCallerDetour(nint a1, nint unused, float timeOffset)
    {
        this.logger.Debug($"ApricotListenerSoundPlayCaller a1:0x{a1:X}, unused:0x{unused:X}, timeOffset:{timeOffset}");
        return ApricotListenerSoundPlayCallerHook.Original(a1, unused, timeOffset);
    }

    private IntPtr PlaySoundDetour(IntPtr a1, IntPtr a2, float a3, int a4, int a5, int a6, int a7, int a8, int a9, int a10, byte a11, uint a12, char a13, int a14, char a15, char a16, char a17, char a18)
    {
        var ret = PlaySoundHook.Original(a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15, a16, a17, a18);
        if (a13 == '\0') { a13 = ' '; }
        if (a15 == '\0') { a15 = ' '; }
        if (a16 == '\0') { a16 = ' '; }
        if (a17 == '\0') { a17 = ' '; }
        if (a18 == '\0') { a18 = ' '; }
        this.logger.Debug($"PlaySound ret:0x{ret:X}, a1:0x{a1:X}, a2:0x{a2:X}, a3:{a3}, a4:{a4}, a5:{a5}, a6:{a6}, a7:{a7}, a8:{a8}, a9:{a9}, a10:{a10}, a11:{a11}, a12:{a12}, a13:{a13}, a14:{a14}, a15:{a15}, a16:{a16}, a17:{a17}, a18:{a18}");
        return ret;
    }

    private static byte[] ReadTerminatedBytes(byte* ptr)
    {
        if (ptr == null) { return []; }

        var bytes = new List<byte>();
        while (*ptr != 0)
        {
            bytes.Add(*ptr);
            ptr += 1;
        }

        return bytes.ToArray();
    }

    private static string ReadTerminatedString(byte* ptr)
    {
        return Encoding.UTF8.GetString(ReadTerminatedBytes(ptr));
    }
}
