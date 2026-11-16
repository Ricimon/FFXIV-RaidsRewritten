// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/ActorVfx.cs
// 5dabc40
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;

namespace RaidsRewritten.Interop.Structs.Vfx;

// Normally this is supposed to use VfxData* but testing has shown that some offsets from VfxObject do apply to actor VFX structs
[StructLayout(LayoutKind.Explicit, Size = 480)]
public struct ActorVfxStruct
{
    [FieldOffset(112)]
    public Vector3 Scale;
}

public unsafe class ActorVfx(ResourceLoader resourceLoader, string path) : BaseVfx(path)
{
    //public VfxData* Vfx;
    public ActorVfxStruct* Vfx;

    public void Create(IGameObject caster, IGameObject target)
    {
        Create((GameObject*)caster.Address, (GameObject*)target.Address);
    }

    public void Create(GameObject* caster, GameObject* target)
    {
        if (Vfx != null) { return; }
        var pathBytes = Encoding.UTF8.GetBytes(Path + '\0');
        fixed (byte* pathPtr = pathBytes)
        {
            Vfx = (ActorVfxStruct*)resourceLoader.ActorVfxCreate(pathPtr, caster, target, -1, (char)0, 0, (char)0);
        }
    }

    // Only some Actor VFX can be scaled
    public void UpdateScale(Vector3 scale)
    {
        if (Vfx == null) { return; }
        Vfx->Scale = new Vector3
        {
            X = scale.X,
            Y = scale.Y,
            Z = scale.Z
        };
    }

    //public virtual void Update()
    //{
    //    if (Vfx == null) { return; }
    //    Vfx->UpdateTransforms(true);
    //    //Vfx->Flags |= 0x2;
    //    // Remove flag that sometimes causes vfx to not appear?
    //    Vfx->SomeFlags &= 0xF7;
    //}

    public override nint GetAddress() => (nint)Vfx;

    public override void Remove()
    {
        if (Vfx == null) { return; }
        resourceLoader.ActorVfxRemove((VfxData*)Vfx, (char)1);
        Vfx = null;
    }
}
