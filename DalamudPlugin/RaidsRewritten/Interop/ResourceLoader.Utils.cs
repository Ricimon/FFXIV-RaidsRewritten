// Adapted from https://github.com/0ceal0t/Dalamud-VFXEditor/blob/main/VFXEditor/Interop/ResourceLoader.Utils.cs
// fbbfedb
namespace RaidsRewritten.Interop;

public unsafe partial class ResourceLoader
{
    private static bool ProcessPenumbraPath(string path, out string outPath)
    {
        outPath = path;
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.StartsWith('|')) return false;

        var split = path.Split("|");
        if (split.Length != 3) return false;

        outPath = split[2];
        return true;
    }
}
