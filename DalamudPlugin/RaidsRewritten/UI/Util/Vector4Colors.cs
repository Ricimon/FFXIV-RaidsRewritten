using System.Numerics;

namespace RaidsRewritten.UI.Util
{
    public sealed class Vector4Colors
    {
        public static Vector4 Red => new(1, 0, 0, 1);
        public static Vector4 Green => new(0, 1, 0, 1);
        public static Vector4 Blue => new(0, 0, 1, 1);
        public static Vector4 DarkBlue => new(0, 0, 0.3f, 1);
        public static Vector4 NeonBlue => new(0.016f, 0.85f, 1, 1);
        public static Vector4 Orange => new(1, 0.65f, 0, 1);
        public static Vector4 DarkRed => new(0.5f, 0, 0, 1);
        public static Vector4 DarkGreen => new(0, 0.5f, 0, 1);
        public static Vector4 White => new(1, 1, 1, 1);
        public static Vector4 Gray => new(0.2f, 0.2f, 0.2f, 1);
        public static Vector4 Black => new(0, 0, 0, 1);
    }
}