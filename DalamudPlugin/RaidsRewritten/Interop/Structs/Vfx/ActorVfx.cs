// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/ActorVfx.cs
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RaidsRewritten.Interop.Structs.Vfx;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ActorVfxStruct
{
    [FieldOffset(0x10)] public float ScaleX;
    [FieldOffset(0x24)] public float ScaleY;
    [FieldOffset(0x38)] public float ScaleZ;
    [FieldOffset(0x40)] public float OffsetX;
    [FieldOffset(0x44)] public float OffsetY;
    [FieldOffset(0x48)] public float OffsetZ;

    // No field from 0x4C to 0x34C seems to affect VFX alpha
}

public unsafe class ActorVfx : BaseVfx
{
    public ActorVfxStruct* Vfx;

    private readonly ResourceLoader resourceLoader;

    public ActorVfx(ResourceLoader resourceLoader, string path) : base(path)
    {
        this.resourceLoader = resourceLoader;
    }

    public void Create(nint casterAddress, nint targetAddress)
    {
        if (Vfx != null) { return; }
        Vfx = (ActorVfxStruct*)this.resourceLoader.ActorVfxCreate(this.Path, casterAddress, targetAddress, -1, (char)0, 0, (char)0);
    }

    public override IntPtr GetVfxPointer()
    {
        return (IntPtr)Vfx;
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        this.resourceLoader.ActorVfxRemove((IntPtr)Vfx, (char)1);
        Vfx = null;
    }

    // Only some Actor VFX can be scaled
    public override void UpdateScale(Vector3 scale)
    {
        if (Vfx == null) { return; }
        Vfx->ScaleX = scale.X;
        Vfx->ScaleY = scale.Y;
        Vfx->ScaleZ = scale.Z;
    }
}
