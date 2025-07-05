using RaidsRewritten.Interop.Structs.Vfx;

namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct Vfx(string Path, BaseVfx? VfxPtr = null, float TimeAlive = 0);
public record struct VfxFadeOut(BaseVfx VfxPtr, float Duration, float TimeRemaining);
