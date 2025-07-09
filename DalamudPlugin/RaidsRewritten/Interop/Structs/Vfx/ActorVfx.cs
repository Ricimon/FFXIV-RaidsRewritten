using System;

namespace RaidsRewritten.Interop.Structs.Vfx;

public unsafe class ActorVfx : BaseVfx
{
    private readonly ResourceLoader resourceLoader;

    public ActorVfx(ResourceLoader resourceLoader, string path) : base(path)
    {
        this.resourceLoader = resourceLoader;
    }

    public void Create(nint casterAddress, nint targetAddress)
    {
        if (Vfx != null) { return; }
        Vfx = (VfxStruct*)this.resourceLoader.ActorVfxCreate(this.Path, casterAddress, targetAddress, -1, (char)0, 0, (char)0);
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        this.resourceLoader.ActorVfxRemove((IntPtr)Vfx, (char)1);
    }
}
