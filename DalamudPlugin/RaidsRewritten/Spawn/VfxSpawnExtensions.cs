using Dalamud.Game.ClientState.Objects.Types;

namespace RaidsRewritten.Spawn;

public static class VfxSpawnExtensions
{
    public static void PlayInvulnerabilityEffect(this VfxSpawn vfxSpawn, IGameObject target)
    {
        if (target == null) { return; }
        vfxSpawn.SpawnActorVfx("vfx/common/eff/dk01gd_inv0h.avfx", target, target);
    }
}
