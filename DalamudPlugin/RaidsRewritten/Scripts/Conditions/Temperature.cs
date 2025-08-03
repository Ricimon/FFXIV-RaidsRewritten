using System;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Temperature(DalamudServices dalamud, ILogger logger) : ISystem
{
    private readonly ILogger logger = logger;
    private Query<Temperature.Component> ComponentQuery;
    public const string GaugeXPositionConfig = "UCOB Rewritten.TemperatureControlX";
    public const string GaugeYPositionConfig = "UCOB Rewritten.TemperatureControlY";
    public const string GaugeImagePath = "temperature_gauge.png";

    public record struct Component()
    {
        public float CurrentTemperature = 0.0f;
        public readonly float MaxTemp = 200.0f;
        public readonly float MinTemp = -100.0f;
        public readonly float OverheatTemp = 100.0f;
        public readonly float DeepfreezeTemp = -100.0f;
    }
    public record struct Overheat();
    public record struct Deepfreeze();
    public record struct Id(int Value);
    public record struct TemperatureDelta(float Value, float ReapplicationCooldownTime, bool SelfDestructable = false, bool Applied = false);
    public static void SetTemperature(Entity playerEntity)
    {
        var world = playerEntity.CsWorld();
        using Query<Temperature.Component> q = playerEntity.CsWorld().QueryBuilder<Temperature.Component>().With(Ecs.ChildOf, playerEntity).Build();
        if (q.IsTrue()) { return; }

        playerEntity.CsWorld().Entity()
                .Set(new Temperature.Component())
                .Set(new Condition.Component("", float.PositiveInfinity))
                .Add<Condition.Hidden>()
                .Add<Condition.IgnoreOnDeath>()
                .ChildOf(playerEntity);
    }
    public static void HeatChangedEvent(Entity playerEntity, float delta, float duration = 0, int id = 0)
    {
        var world = playerEntity.CsWorld();
        using Query<Temperature.Component> q = playerEntity.CsWorld().QueryBuilder<Temperature.Component>().With(Ecs.ChildOf, playerEntity).Build();
        if (!q.IsTrue()) { return; }
        
        Entity te = default;
        q.Each((Entity e, ref Temperature.Component tc) => {
            te = e;
        });
        
        if (id != 0)
        {
            Entity existingDelta = default;
            using var tq = world.QueryBuilder<TemperatureDelta, Id>().With(Ecs.ChildOf, te).Build();
            tq.Each((Entity e, ref TemperatureDelta td, ref Id idx) =>
            {
                if (idx.Value == id)
                {
                    existingDelta = e;
                    //Refresh Logic
                    if (td.ReapplicationCooldownTime <= 0)
                    {
                        td.ReapplicationCooldownTime += duration;
                        td.Applied = false;
                        td.SelfDestructable = false;
                    }
                }
            });

            if (existingDelta.IsValid())
            {
                return;
            }
            else
            {
                world.Entity()
                    .Set(new TemperatureDelta(delta, duration))
                    .Set(new Id(id))
                    .ChildOf(te);
                return;
            }
        }
        else 
        {
            world.Entity()
                .Set(new TemperatureDelta (delta, duration))
                .ChildOf(te);
            return;
        }

    }

    public void Register(World world)
    {

        world.System<Temperature.Component, TemperatureDelta>()
            .TermAt(0).Up()
            .Each((Iter it, int i, ref Temperature.Component temperature, ref TemperatureDelta delta) =>
            {
                try
                {
                    if (!delta.Applied)
                    {
                        delta.Applied = true;
                        temperature.CurrentTemperature += delta.Value;
                        temperature.CurrentTemperature = Math.Clamp(temperature.CurrentTemperature, temperature.MinTemp, temperature.MaxTemp); ;
                        //logger.Info(temperature.CurrentTemperature.ToString());
                    }

                    if (delta.SelfDestructable)
                    {
                        var e = it.Entity(i);
                        e.Destruct();
                    }

                    delta.ReapplicationCooldownTime -= it.DeltaTime();

                    if (delta.ReapplicationCooldownTime <= 0)
                    {
                        delta.SelfDestructable = true;
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });

        world.System<Temperature.Component>()
            .Each((Iter it, int i, ref Component Temperature) => 
            {
                var e = it.Entity(i);
                if (!e.Parent().Has<Player.Component>()) { return; }
                if (!e.Parent().Get<Player.Component>().IsLocalPlayer) { return; }

                var player = dalamud.ClientState.LocalPlayer;
                if (player != null && player.IsDead)
                {
                    Temperature.CurrentTemperature = 0;
                }
                try
                {
                    if (Temperature.CurrentTemperature <= Temperature.DeepfreezeTemp)
                    {
                        if (!e.Has<Deepfreeze>())
                        {
                            e.Remove<Overheat>();
                            e.Remove<ActorVfx>();

                            e.Add<Deepfreeze>();
                            e.Set(new ActorVfx("vfx/common/eff/hyouketu0f.avfx"));
                            //logger.Info("Deepfreeze");
                        }
                        return;
                    }
                    if (Temperature.CurrentTemperature >= Temperature.OverheatTemp)
                    {
                        if (!e.Has<Overheat>())
                        { 
                            e.Remove<Deepfreeze>();
                            e.Remove<ActorVfx>();

                            e.Add<Overheat>(); 
                            e.Set(new ActorVfx("vfx/common/eff/dk10ht_hea0s.avfx"));
                            //logger.Info("Overheat");
                        }
                        return;
                    }
                    e.Remove<Deepfreeze>();
                    e.Remove<Overheat>();
                    e.Remove<ActorVfx>();
                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToStringFull());
                }
            });
    }
}