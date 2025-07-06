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
}
