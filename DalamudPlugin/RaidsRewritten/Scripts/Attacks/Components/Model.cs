namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct Model(int ModelCharaId, bool Spawned = false, uint GameObjectIndex = default, bool DrawEnabled = false);
public record struct ModelFadeOut(uint GameObjectIndex, float Duration, float TimeRemaining);
