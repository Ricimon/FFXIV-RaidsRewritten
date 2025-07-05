using RaidsRewritten.Interop.Structs.Vfx;

namespace RaidsRewritten.Scripts.Attacks.Components;

public record struct Vfx(string Path, BaseVfx? VfxPtr = null);
public record struct VfxFadeOut(BaseVfx VfxPtr, float Duration, float TimeRemaining);
