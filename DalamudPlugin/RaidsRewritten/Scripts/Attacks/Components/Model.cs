using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct Model(
    int ModelCharaId,
    bool Spawned = false,
    uint GameObjectIndex = default,
    bool DrawEnabled = false,
    IGameObject? GameObject = null);

public record struct ModelFadeOut(uint GameObjectIndex, float Duration, float TimeRemaining);

public record struct ModelTimelineSpeed(float Value);
