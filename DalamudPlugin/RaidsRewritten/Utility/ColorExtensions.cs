using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace RaidsRewritten.Utility;

public static class ColorExtensions
{
    public static uint ToColorU32(this Vector4 v)
    {
        return ImGui.ColorConvertFloat4ToU32(v);
    }

    public static uint ToColorU32(this Vector3 v)
    {
        var v4 = new Vector4(v.X, v.Y, v.Z, 1);
        return ToColorU32(v4);
    }

    public static Vector4 WithAlpha(this Vector4 v, float alpha)
    {
        return new Vector4(v.X, v.Y, v.Z, alpha);
    }
}
