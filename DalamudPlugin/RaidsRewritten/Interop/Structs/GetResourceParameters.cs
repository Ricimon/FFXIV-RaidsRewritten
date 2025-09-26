// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/Structs/GetResourceParameters.cs
using System.Runtime.InteropServices;

namespace RaidsRewritten.Interop.Structs;

[StructLayout( LayoutKind.Explicit )]
public struct GetResourceParameters {
    [FieldOffset( 16 )]
    public uint SegmentOffset;

    [FieldOffset( 20 )]
    public uint SegmentLength;

    public readonly bool IsPartialRead
        => SegmentLength != 0;
}
