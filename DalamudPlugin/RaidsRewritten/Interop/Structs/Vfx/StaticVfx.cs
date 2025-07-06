using System;
using System.Numerics;

namespace RaidsRewritten.Interop.Structs.Vfx;

public unsafe class StaticVfx : BaseVfx
{
    private readonly ResourceLoader resourceLoader;

    public StaticVfx(ResourceLoader resourceLoader, string path) : base(path)
    {
        this.resourceLoader = resourceLoader;
    }

    public void Create(Vector3 position, float rotation)
    {
        if (Vfx != null) { return; }
        Vfx = (VfxStruct*)this.resourceLoader.StaticVfxCreate(this.Path, "Client.System.Scheduler.Instance.VfxObject");

        UpdatePosition(position);
        UpdateRotation(new Vector3(0, 0, rotation));
        Update();

        this.resourceLoader.StaticVfxRun((IntPtr)Vfx, 0.0f, 0xFFFFFFFF);
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        this.resourceLoader.StaticVfxRemove((IntPtr)Vfx);
        Vfx = null;
    }
}
