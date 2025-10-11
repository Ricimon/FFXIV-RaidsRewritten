// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/BaseVfx.cs
// 16ae60f

using System;
using System.Numerics;

namespace RaidsRewritten.Interop.Structs.Vfx;

/*
    *(undefined4 *)(vfx + 0x50) = DAT_01bb2850;
    *(undefined4 *)(vfx + 0x54) = DAT_01bb2854;
    *(undefined4 *)(vfx + 0x58) = DAT_01bb2858;
    uVar3 = uRam0000000001bb286c;
    uVar2 = uRam0000000001bb2868;
    uVar5 = uRam0000000001bb2864;
    *(undefined4 *)(vfx + 0x60) = _ZERO_VECTOR;
    *(undefined4 *)(vfx + 100) = uVar5;
    *(undefined4 *)(vfx + 0x68) = uVar2;
    *(undefined4 *)(vfx + 0x6c) = uVar3;
    *(undefined4 *)(vfx + 0x70) = DAT_01bb2870;
    *(undefined4 *)(vfx + 0x74) = DAT_01bb2874;
    uVar5 = DAT_01bb2878;
    *(undefined4 *)(vfx + 0x78) = DAT_01bb2878;
    *(ulonglong *)(vfx + 0x38) = *(ulonglong *)(vfx + 0x38) | 2;
    * + 0x43 for the color (targeting vfx)
    * vfxColor = vfx + 0x45
    * 
 */

public abstract unsafe class BaseVfx
{
    public string Path;

    public BaseVfx(string path)
    {
        Path = path;
    }

    public abstract IntPtr GetVfxPointer();

    public abstract void Remove();

    public abstract void UpdateScale(Vector3 scale);
}
