namespace RaidsRewritten.Scripts.Components;

public record struct MouseLeftState(bool IsPressed, bool IsPressedThisTick);
public record struct MouseRightState(bool IsPressed, bool IsPressedThisTick);

public record struct PlaceMechanicWithMouse(float ReticleRadius);
public struct PlacementReticle;
