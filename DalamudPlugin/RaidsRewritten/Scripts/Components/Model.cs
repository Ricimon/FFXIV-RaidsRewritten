using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Scripts.Components;

public record struct Model(
    int ModelCharaId,
    bool Spawned = false,
    uint GameObjectIndex = default,
    bool DrawEnabled = false,
    IGameObject? GameObject = null);

public record struct NpcEquipRow(uint Value);

public record struct ModelFadeOut(uint GameObjectIndex, float Duration, float TimeRemaining, float Alpha = 1f);

public record struct OneTimeModelTimeline(ushort Id, bool Played = false);

public record struct ModelTimelineSpeed(float Value);

public record struct TimelineBase(ushort Value, bool Interrupt = false);

// this only applies on model creation. will do more research if we ever need to update after model has already spawned
public record struct AnimationState(byte Value1, byte Value2 = 0);
