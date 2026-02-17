using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RaidsRewritten.Interop.Structs;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct AtkComponentIconText
{
    [FieldOffset(216)] public uint IconId;
}
