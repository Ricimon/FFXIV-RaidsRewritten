namespace RaidsRewritten.Scripts.Components;

public record struct Model(
    int ModelCharaId,
    bool Spawned = false,
    ushort ObjectIndex = default,
    bool DrawEnabled = false);

public record struct NpcEquipRow(uint Value);

public record struct ModelFadeOut(ushort ObjectIndex, float Duration, float TimeRemaining, float Alpha = 1f);

public record struct OneTimeModelTimeline(ushort Id, bool Played = false);

public record struct ModelTimelineSpeed(float Value);

public record struct TimelineBase(ushort Value, bool Interrupt = false);
public record struct TimelineBlend(uint Slot, ushort Value);

// this only applies on model creation. will do more research if we ever need to update after model has already spawned
public record struct AnimationState(byte Value1, byte Value2 = 0);

public record struct ModelHeight(float Value);

public record struct ChatBubble(string Text, float PlayDuration = 3);
