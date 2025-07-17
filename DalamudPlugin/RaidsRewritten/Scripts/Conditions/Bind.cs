using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Interop.Structs.Vfx;
using RaidsRewritten.Log;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts.Conditions;

public class Bind(DalamudServices dalamud, VfxSpawn vfxSpawn, ILogger logger) : ISystem
{
    public record struct Component(BaseVfx? Vfx = null);

    public static void ApplyToPlayer(Entity playerEntity, float duration)
    {
        playerEntity.CsWorld().Entity()
            .Set(new Condition.Component("Bound", duration))
            .Set(new Component())
            .ChildOf(playerEntity);
    }

    public void Register(World world)
    {
        world.System<Condition.Component, Component>()
            .Each((Iter it, int i, ref Condition.Component condition, ref Component bound) =>
            {
                var e = it.Entity(i);
                if (!e.Parent().Has<Player.Component>()) { return; }
                if (!e.Parent().Get<Player.Component>().IsLocalPlayer) { return; }

                var localPlayer = dalamud.ClientState.LocalPlayer;
                //if (bound.Vfx == null && localPlayer != null)
                //{
                //    bound.Vfx = this.vfxSpawn.SpawnActorVfx("vfx/common/eff/dk05ht_bind0t.avfx", localPlayer, localPlayer);
                //}
            });
    }
}
