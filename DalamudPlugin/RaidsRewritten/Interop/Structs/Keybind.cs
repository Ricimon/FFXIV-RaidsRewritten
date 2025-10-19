// Adapted from https://github.com/Infiziert90/ChatTwo/blob/main/ChatTwo/GameFunctions/Types/Keybind.cs
// c24ca3c
using Dalamud.Game.ClientState.Keys;

namespace RaidsRewritten.Interop.Structs;

internal class Keybind
{
    internal VirtualKey Key1 { get; init; }
    internal ModifierFlag Modifier1 { get; init; }

    internal VirtualKey Key2 { get; init; }
    internal ModifierFlag Modifier2 { get; init; }
}
