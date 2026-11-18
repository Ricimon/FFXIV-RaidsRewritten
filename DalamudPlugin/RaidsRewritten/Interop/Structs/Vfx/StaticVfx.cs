// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/StaticVfx.cs
// 5dabc40
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace RaidsRewritten.Interop.Structs.Vfx;

public unsafe class StaticVfx(ResourceLoader resourceLoader, string path) : BaseVfx(path)
{
    public VfxObject* Vfx;

    public void Create(Vector3 position, float rotation)
    {
        if (Vfx != null) { return; }
        Vfx = VfxObject.Create(Path, "Client.System.Scheduler.Instance.VfxObject");

        resourceLoader.StaticVfxRun(Vfx, 0.0f, 0xFFFFFFFF);

        UpdatePosition(position);
        UpdateRotation(rotation);
        Update();
    }

    public void UpdatePosition(Vector3 position)
    {
        if (Vfx == null) { return; }
        Vfx->Position = new Vector3
        {
            X = position.X,
            Y = position.Y,
            Z = position.Z
        };
    }

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

    public void UpdateRotation(float rotation)
    {
        if (Vfx == null) { return; }

        Vfx->Rotation = FFXIVClientStructs.FFXIV.Common.Math.Quaternion.CreateFromYawPitchRoll(
            rotation,
            0,
            0
        );
    }

    public void UpdateRotation(Quaternion rotation)
    {
        if (Vfx == null) { return; }

        Vfx->Rotation = rotation;
    }

    public void UpdateColor(Vector4 color)
    {
        if (Vfx == null) { return; }
        Vfx->Color = color;
    }

    public void UpdateAlpha(float alpha)
    {
        if (Vfx == null) { return; }
        Vfx->Color.W = alpha;
        //Vfx->SetTransparency(alpha); // this seems to do nothing
    }

    public void Update()
    {
        if (Vfx == null) { return; }
        Vfx->UpdateTransforms(true);
        //Vfx->Flags |= 0x2;
        // Remove flag that sometimes causes vfx to not appear?
        Vfx->SomeFlags &= 0xF7;
    }

    public override nint GetAddress() => (nint)Vfx;

    public override void Remove()
    {
        if (Vfx == null) { return; }
        resourceLoader.StaticVfxRemove(Vfx);
        Vfx = null;
    }
}
