using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace RaidsRewritten.Interop.Structs;

internal class Keybind
{
    internal VirtualKey Key1 { get; init; }
    internal ModifierFlag Modifier1 { get; init; }

    internal VirtualKey Key2 { get; init; }
    internal ModifierFlag Modifier2 { get; init; }
}
