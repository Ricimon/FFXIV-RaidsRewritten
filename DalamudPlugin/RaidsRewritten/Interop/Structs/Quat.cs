// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/Quat.cs
// 4619855
using System.Numerics;
using System.Runtime.InteropServices;

namespace RaidsRewritten.Interop.Structs;

[StructLayout( LayoutKind.Sequential )]
public struct Quat {
    public float X;
    public float Z;
    public float Y;
    public float W;

    public static implicit operator Vector4( Quat pos ) => new( pos.X, pos.Y, pos.Z, pos.W );
}
