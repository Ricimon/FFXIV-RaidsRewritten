using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Scripts.Components;

public record struct StaticVfx(string Path, Interop.Structs.Vfx.StaticVfx? VfxPtr = null);
public record struct ActorVfx(string Path, Interop.Structs.Vfx.ActorVfx? VfxPtr = null);
public record struct ActorVfxSource(IGameObject? Source);
public record struct ActorVfxTarget(IGameObject? Target);
public record struct VfxId(BigInteger Value);
public record struct Alpha(float Value);
public record struct VfxFadeOut(Interop.Structs.Vfx.StaticVfx VfxPtr, float Duration, float TimeRemaining);
public record struct VfxFadeOutDuration(float Value);
