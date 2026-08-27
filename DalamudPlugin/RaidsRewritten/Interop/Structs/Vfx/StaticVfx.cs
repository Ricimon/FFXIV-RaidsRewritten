// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/StaticVfx.cs
// 08de12a
using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.System.String;

namespace RaidsRewritten.Interop.Structs.Vfx;

public unsafe class StaticVfx(ResourceLoader resourceLoader, string path) : BaseVfx(path)
{
    public void Create(Vector3 position, float rotation)
    {
        if (Vfx != null) { return; }
        Vfx = resourceLoader.StaticVfxCreate(
            (new Utf8String(this.Path)).StringPtr,
            (new Utf8String("Client.System.Scheduler.Instance.VfxObject")).StringPtr
        );

        resourceLoader.StaticVfxRun(Vfx, 0.0f, 0xFFFFFFFF);

        UpdatePosition(position);
        UpdateRotation(rotation);
        Update();
    }

    public override IntPtr GetVfxPointer()
    {
        return (IntPtr)Vfx;
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        resourceLoader.StaticVfxRemove(Vfx);
        Vfx = null;
    }
}
