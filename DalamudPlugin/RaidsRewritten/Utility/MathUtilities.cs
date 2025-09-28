using ECommons.MathHelpers;
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

    public static float GetAngleBetweenLines(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var cosVal = ((a2.X - a1.X) * (b2.X - b1.X) + (a2.Y - a1.Y) * (b2.Y - b1.Y)) /
                (MathF.Sqrt(MathHelper.Square(a2.X - a1.X) + MathHelper.Square(a2.Y - a1.Y)) * MathF.Sqrt(MathHelper.Square(b2.X - b1.X) + MathHelper.Square(b2.Y - b1.Y)));
        return MathF.Acos(MathF.Round(cosVal * 10000) / 10000);
    }

    public static float GetAbsoluteAngleFromSourceToTarget(Vector3 sourcePos, Vector3 targetPos)
    {
        var sourcePosV2 = new Vector2(sourcePos.X, sourcePos.Z);
        var targetPosV2 = new Vector2(targetPos.X, targetPos.Z);
        return GetAbsoluteAngleFromSourceToTarget(sourcePosV2, targetPosV2);
    }

    public static float GetAbsoluteAngleFromSourceToTarget(Vector2 sourcePos, Vector2 targetPos)
    {
        var north = new Vector2(sourcePos.X, sourcePos.Y + 1);
        var angle = GetAngleBetweenLines(sourcePos, north, sourcePos, targetPos);
        if (float.IsNaN(angle)) return 0;
        if (targetPos.X < sourcePos.X ) { return -angle; }
        return angle;
    }
}
