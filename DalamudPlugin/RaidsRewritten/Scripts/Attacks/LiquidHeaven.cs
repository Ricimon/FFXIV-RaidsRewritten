using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;

namespace RaidsRewritten.Scripts.Attacks;

public class LiquidHeaven(DalamudServices dalamud, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float Cooldown);

    private const float HitBoxRadius = 5f;
    private const float HeatValue = -25.0f;
    private const float HitCooldown = 1.0f;
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
        world.System<Component, Position>()
            .Each((Iter it, int i, ref Component component, ref Position position) =>
            {
                try
                {
                    component.Cooldown = Math.Max(component.Cooldown - it.DeltaTime(), 0);
                    var player = dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }
                    if (component.Cooldown > 0) { return; }

                    if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) <= HitBoxRadius)
                    {
                        component.Cooldown = HitCooldown;
                        using var q = Player.Query(it.World());
                        q.Each((Entity e, ref Player.Component pc) =>
                        {
                            Temperature.HeatChangedEvent(e, HeatValue);
                        });
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }
}