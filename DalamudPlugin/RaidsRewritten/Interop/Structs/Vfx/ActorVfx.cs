// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/ActorVfx.cs
// 08de12a
using System;

namespace RaidsRewritten.Interop.Structs.Vfx;

public unsafe class ActorVfx(ResourceLoader resourceLoader, string path) : BaseVfx(path)
{
    public void Create(IntPtr caster, IntPtr target)
    {
        if (Vfx != null) { return; }
        Vfx = resourceLoader.ActorVfxCreate(this.Path, caster, target, -1, (char)0, 0, (char)0);
    }

    public override IntPtr GetVfxPointer()
    {
        return (IntPtr)Vfx;
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        resourceLoader.ActorVfxRemove(Vfx, (char)1);
        Vfx = null;
    }
}
