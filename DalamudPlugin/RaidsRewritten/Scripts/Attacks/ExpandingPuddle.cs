using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class ExpandingPuddle(DalamudServices dalamud, CommonQueries commonQueries) : IAttack, ISystem
{
    public record struct Component(string VfxPath, float ElapsedTime = 0);

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new StaticVfx("bgcommon/world/common/vfx_for_btl/b0195/eff/b0195_yuka_c.avfx"))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component>().Without<StaticVfx>()
            .Each((Iter it, int i, ref Component component) =>
            {
                if (!string.IsNullOrEmpty(component.VfxPath))
                {
                    it.Entity(i).Set(new StaticVfx(component.VfxPath));
                }
            });

        world.System<Component, Position>()
            .Each((Iter it, int i, ref Component component, ref Position position) =>
            {
                //try
                //{
                //    var player = dalamud.ClientState.LocalPlayer;
                //    if (player == null || player.IsDead) { return; }

                //    if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) <= HitBoxRadius)
                //    {
                //        this.playerQuery.Each((Entity e, ref Player.Component pc) => 
                //        {
                //            Temperature.HeatChangedEvent(e, HeatValue, HitCooldown, HeavenID);
                //        });
                //    }
                //}
                //catch (Exception e)
                //{
                //    logger.Error(e.ToStringFull());
                //}
            });
    }
}
