using System;
using System.Numerics;

namespace RaidsRewritten.Utility;

public static class MathUtilities
{
    /// <summary>
    /// rotation input is in FFXIV units
    /// </summary>
    public static Vector2 RotationToUnitVector(float rotation)
    {
        return new Vector2(MathF.Sin(rotation), MathF.Cos(rotation));
    }

    /// <summary>
    /// rotation output is in FFXIV units
    /// </summary>
    public static float VectorToRotation(Vector2 vector)
    {
        return MathF.Atan2(vector.X, vector.Y);
    }

    /// <summary>
    /// Clamps between -π to +π
    /// </summary>
    public static float ClampRadians(float radians)
    {
        radians %= 2 * MathF.PI;
        if (Math.Abs(radians) > MathF.PI)
        {
            radians = -1 * (Math.Sign(radians) * 2 * MathF.PI - radians);
        }
        return radians;
    }

    public static Vector2 Rotate(Vector2 v, float radians)
    {
        var c = MathF.Cos(radians);
        var s = MathF.Sin(radians);
        return new Vector2(c * v.X - s * v.Y, s * v.X + c * v.Y);
    }
}
