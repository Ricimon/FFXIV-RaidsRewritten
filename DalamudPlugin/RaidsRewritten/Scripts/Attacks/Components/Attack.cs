namespace RaidsRewritten.Scripts.Attacks.Components;

public struct Attack;
public struct Omen;
public record struct OmenDuration(float Duration, bool AutoDestruct, float ElapsedTime = 0f);
public record struct FadeOmen(float Duration, float ElapsedTime = 0f);
