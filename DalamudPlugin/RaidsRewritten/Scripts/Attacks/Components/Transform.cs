using System.Numerics;

namespace RaidsRewritten.Scripts.Attacks.Components;

public struct Transform
{
    public Vector3 Position;
    public float Rotation;
    public Vector3 Scale;

    public Transform(Vector3 position, float rotation)
    {
        this.Position = position;
        this.Rotation = rotation;
    }

    public Transform(Vector3 position, float rotation, Vector3 scale)
    {
        this.Position = position;
        this.Rotation = rotation;
        this.Scale = scale;
    }
}
