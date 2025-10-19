using System;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Conditions;

public class Temperature(DalamudServices dalamud, CommonQueries commonQueries, ILogger logger) : ISystem, IDisposable
{
    private readonly ILogger logger = logger;
    private Query<Overheat.Component> overheatQuery;
    private Query<Deepfreeze.Component> deepfreezeQuery;

    public const string GaugeXPositionConfig = "UCOB Rewritten.TemperatureControlX";
    public const string GaugeYPositionConfig = "UCOB Rewritten.TemperatureControlY";
    public const string GaugeScaleConfig = "UCOB Rewritten.TemperatureControlScale";
    public const string GaugeImagePath = "temperature_gauge.png";
    private const int TemperatureID = 420;
    public record struct Component()
    {
        public float CurrentTemperature = 0.0f;
        public readonly float MaxTemp = 200.0f;
        public readonly float MinTemp = -100.0f;
        public readonly float OverheatTemp = 100.0f;
        public readonly float DeepfreezeTemp = -100.0f;
    }
    public record struct Id(int Value);
    public record struct TemperatureDelta(float Value, float ReapplicationCooldownTime, bool SelfDestructable = false, bool Applied = false);
    public static void SetTemperature(Entity playerEntity)
    {
        var world = playerEntity.CsWorld();
        using Query<Temperature.Component> q = world.QueryBuilder<Temperature.Component>().With(Ecs.ChildOf, playerEntity).Build();
        if (q.IsTrue()) { return; }

        world.Entity()
            .Set(new Temperature.Component())
            .Set(new Condition.Component("", float.PositiveInfinity, DateTime.UtcNow))
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
    public void Dispose()
    {
        overheatQuery.Dispose();
        deepfreezeQuery.Dispose();
    }

    public void Register(World world)
    {
        this.overheatQuery  = world.QueryBuilder<Overheat.Component>().Build();
        this.deepfreezeQuery = world.QueryBuilder<Deepfreeze.Component>().Build();

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
            .With<Player.Component>().Up()
            .With<Player.LocalPlayer>().Up()
            .Each((Iter it, int i, ref Component Temperature) => 
            {
                var e = it.Entity(i);

                var player = dalamud.ClientState.LocalPlayer;
                if (player != null && player.IsDead)
                {
                    Temperature.CurrentTemperature = 0;
                }
                try
                {
                    if (Temperature.CurrentTemperature <= Temperature.DeepfreezeTemp)
                    {
                        if(deepfreezeQuery.IsTrue()) { return; }
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                        {
                            world.DeleteWith<Overheat.Component>();
                            Deepfreeze.ApplyToTarget(e, float.PositiveInfinity, TemperatureID);
                        });
                        return;
                    }
                    if (Temperature.CurrentTemperature >= Temperature.OverheatTemp)
                    {
                        if (overheatQuery.IsTrue()) { return; }
                        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component pc) =>
                        {
                            world.DeleteWith<Deepfreeze.Component>();
                            Overheat.ApplyToTarget(e, float.PositiveInfinity, TemperatureID);
                        });
                        return;
                    }
                    world.DeleteWith<Deepfreeze.Component>();
                    world.DeleteWith<Overheat.Component>();
                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToStringFull());
                }
            });
    }
}