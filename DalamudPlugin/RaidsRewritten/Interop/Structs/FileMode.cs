// Adapted from https://github.com/xivdev/Penumbra/blob/master/Penumbra/Interop/Structs/FileMode.cs
// 2b4a01d
namespace RaidsRewritten.Interop.Structs;

public enum FileMode : byte
{
    LoadUnpackedResource = 0,
    LoadFileResource     = 1, // The config files in MyGames use this.

    // Probably debug options only.
    LoadIndexResource  = 0xA, // load index/index2
    LoadSqPackResource = 0xB,
}
