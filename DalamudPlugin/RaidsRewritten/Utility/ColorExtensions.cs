using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RaidsRewritten.Extensions;

public static class ColorExtensions
{
    public static uint ToColorU32(this Vector4 v)
    {
        return ImGui.ColorConvertFloat4ToU32(v);
    }

    public static Vector4 WithAlpha(this Vector4 v, float alpha)
    {
        return new Vector4(v.X, v.Y, v.Z, alpha);
    }
}
