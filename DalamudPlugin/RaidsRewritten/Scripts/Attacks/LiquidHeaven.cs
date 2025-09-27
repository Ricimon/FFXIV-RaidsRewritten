using System;
using System.Numerics;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class LiquidHeaven(DalamudServices dalamud, CommonQueries commonQueries, ILogger logger) : IAttack, ISystem
{
    public record struct Component(float Cooldown);

    private const float HitBoxRadius = 5f;
    private const float HeatValue = -1.0f;
    private const float HitCooldown = 0.1f;
    private const int HeavenID = 1234; //Sample numbers, could also use the entity id maybe
    private const int UcobNaelWeather = 21;

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
                    var entity = it.Entity(i);
                    unsafe
                    {
                        // Specifically in UCOB Nael phase night time, the puddle VFX is too bright
                        float alpha = 1.0f;
                        var weatherManager = WeatherManager.Instance();
                        var framework = Framework.Instance();
                        if (weatherManager != null && framework != null)
                        {
                            var weather = weatherManager->GetCurrentWeather();
                            var et = framework->ClientTime.GetEorzeaTimeOfDay();
                            var night = et < TimeSpan.FromHours(6) || et >= TimeSpan.FromHours(18);
                            if (weather == UcobNaelWeather && night)
                            {
                                alpha = 0.4f;
                            }
                        }
                        entity.Set(new Alpha(alpha));
                    }

                    var player = dalamud.ClientState.LocalPlayer;
                    if (player == null || player.IsDead) { return; }

                    if (Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2()) <= HitBoxRadius)
                    {
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) => 
                        {
                            Temperature.HeatChangedEvent(e, HeatValue, HitCooldown, HeavenID);
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