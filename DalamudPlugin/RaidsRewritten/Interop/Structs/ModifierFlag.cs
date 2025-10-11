// Adapted from https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/GameFunctions/Types/ModifierFlag.cs
// 352088d
using System;

namespace RaidsRewritten.Interop.Structs;

[Flags]
internal enum ModifierFlag
{
    None = 0,
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
}
