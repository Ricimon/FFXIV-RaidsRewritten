using Flecs.NET.Core;
using RaidsRewritten.Scripts.Conditions;
using static RaidsRewritten.Memory.StatusFlyPopupTextProcessor;

namespace RaidsRewritten.Scripts.Components;

public record struct FlyText(Entity LinkedStatusEntity, Condition.Status Status, bool IsEnfeeblement);
public record struct FlyTextReady(FlyPopupTextData Data);
