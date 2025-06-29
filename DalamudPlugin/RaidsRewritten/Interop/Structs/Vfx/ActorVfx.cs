using System;
using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Interop.Structs.Vfx;

public unsafe class ActorVfx : BaseVfx
{
    private readonly ResourceLoader resourceLoader;

    public ActorVfx(ResourceLoader resourceLoader, string path) : base(path)
    {
        this.resourceLoader = resourceLoader;
    }

    public void Create(IGameObject caster, IGameObject target)
    {
        if (Vfx != null) { return; }
        Vfx = (VfxStruct*)this.resourceLoader.ActorVfxCreate(this.Path, caster.Address, target.Address, -1, (char)0, 0, (char)0);
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        this.resourceLoader.ActorVfxRemove((IntPtr)Vfx, (char)1);
    }
}
