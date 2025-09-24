using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Interop.Structs.Vfx;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct StaticVfxStruct
{
    [FieldOffset(0x38)] public byte Flags;
    [FieldOffset(0x50)] public Vector3 Position;
    [FieldOffset(0x60)] public Quat Rotation;
    [FieldOffset(0x70)] public Vector3 Scale;

    [FieldOffset(0x128)] public int ActorCaster;
    [FieldOffset(0x130)] public int ActorTarget;

    [FieldOffset(0x1B8)] public int StaticCaster;
    [FieldOffset(0x1C0)] public int StaticTarget;

    [FieldOffset(0x248)] public byte SomeFlags;

    [FieldOffset(0x260)] public byte Red;
    [FieldOffset(0x264)] public byte Green;
    [FieldOffset(0x268)] public byte Blue;
    [FieldOffset(0x26C)] public float Alpha;
}

public unsafe class StaticVfx : BaseVfx
{
    public StaticVfxStruct* Vfx;

    private readonly ResourceLoader resourceLoader;

    public StaticVfx(ResourceLoader resourceLoader, string path) : base(path)
    {
        this.resourceLoader = resourceLoader;
    }

    public void Create(Vector3 position, float rotation)
    {
        if (Vfx != null) { return; }
        Vfx = (StaticVfxStruct*)this.resourceLoader.StaticVfxCreate(this.Path, "Client.System.Scheduler.Instance.VfxObject");

        UpdatePosition(position);
        UpdateRotation(new Vector3(0, 0, rotation));
        Update();

        this.resourceLoader.StaticVfxRun((IntPtr)Vfx, 0.0f, 0xFFFFFFFF);
    }

    public override IntPtr GetVfxPointer()
    {
        return (IntPtr)Vfx;
    }

    public override void Remove()
    {
        if (Vfx == null) { return; }
        this.resourceLoader.StaticVfxRemove((IntPtr)Vfx);
        Vfx = null;
    }

    public override void UpdateScale(Vector3 scale)
    {
        if (Vfx == null) { return; }
        Vfx->Scale = new Vector3
        {
            X = scale.X,
            Y = scale.Y,
            Z = scale.Z
        };
    }

    public void Update()
    {
        if (Vfx == null) { return; }
        Vfx->Flags |= 0x2;
        // Remove flag that sometimes causes vfx to not appear?
        Vfx->SomeFlags &= 0xF7;
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

    public void UpdatePosition(IGameObject actor)
    {
        if (Vfx == null) { return; }
        Vfx->Position = actor.Position;
    }

    public void UpdateRotation(Vector3 rotation)
    {
        if (Vfx == null) { return; }

        var q = Quaternion.CreateFromYawPitchRoll(rotation.X, rotation.Y, rotation.Z);
        Vfx->Rotation = new Quat
        {
            X = q.X,
            Y = q.Y,
            Z = q.Z,
            W = q.W
        };
    }

    public void UpdateAlpha(float alpha)
    {
        if (Vfx == null) { return; }
        Vfx->Alpha = alpha;
    }
}
