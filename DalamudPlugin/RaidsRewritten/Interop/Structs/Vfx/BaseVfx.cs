// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Vfx/BaseVfx.cs
// 08de12a
using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace RaidsRewritten.Interop.Structs.Vfx;

public abstract unsafe class BaseVfx
{
    public VfxObject* Vfx;
    public string Path;

    public BaseVfx(string path)
    {
        Path = path;
    }

    public abstract IntPtr GetVfxPointer();

    public abstract void Remove();

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

    public void UpdatePosition(IGameObject actor)
    {
        if (Vfx == null) { return; }
        Vfx->Position = actor.Position;
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

    public void UpdateRotation(float rotation)
    {
        if (Vfx == null) { return; }

        Vfx->Rotation = FFXIVClientStructs.FFXIV.Common.Math.Quaternion.CreateFromYawPitchRoll(
            rotation,
            0,
            0
        );
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
    }
}
