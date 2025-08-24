using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class ExpandingPuddle(DalamudServices dalamud, CommonQueries commonQueries) : IAttack, ISystem
{
    public record struct Component(
        string VfxPath,
        float StartScale,
        float EndScale,
        float ExpandSpeed,
        float Lifetime,
        float ElapsedTime = 0);

    public Entity Create(World world)
    {
        return world.Entity()
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

        world.System<Component, Position, Scale>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Scale scale) =>
            {
                component.ElapsedTime += it.DeltaTime();

                var targetScale = component.ElapsedTime * component.ExpandSpeed + component.StartScale;
                if (targetScale > component.EndScale)
                {
                    targetScale = component.EndScale;
                }

                scale.Value = targetScale * Vector3.One;

                if (component.ElapsedTime > component.Lifetime)
                {
                    it.Entity(i).Destruct();
                }

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
