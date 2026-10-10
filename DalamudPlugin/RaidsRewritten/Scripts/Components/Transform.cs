using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Scripts.Components;

public record struct Position(Vector3 Value);
public record struct Rotation(float Value);
public record struct Scale(Vector3 Value);
public record struct UniformScale(float Value);

public record struct LocalPosition(Vector3 Value);
public record struct FullRotation(Quaternion Value);

public record struct AngularVelocity(float Value);
public record struct FullAngularVelocity(Vector3 Value);

public record struct FollowPosition(IGameObject Target);
