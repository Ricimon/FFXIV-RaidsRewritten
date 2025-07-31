using Dalamud.Game.ClientState.Objects.Types;
using RaidsRewritten.Interop.Structs.Vfx;

namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct StaticVfx(string Path, Interop.Structs.Vfx.StaticVfx? VfxPtr = null);
public record struct ActorVfx(string Path, Interop.Structs.Vfx.ActorVfx? VfxPtr = null);
public record struct ActorVfxTarget(IGameObject? Target);
public record struct VfxFadeOut(BaseVfx VfxPtr, float Duration, float TimeRemaining);
