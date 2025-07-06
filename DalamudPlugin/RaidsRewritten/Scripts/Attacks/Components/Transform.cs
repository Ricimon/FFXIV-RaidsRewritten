using System.Numerics;

namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct Position(Vector3 Value);
public record struct Rotation(float Value);
public record struct Scale(Vector3 Value);
public record struct UniformScale(float Value);
