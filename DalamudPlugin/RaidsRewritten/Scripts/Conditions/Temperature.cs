using System;
using System.Net;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using static Flecs.NET.Core.Ecs.Units;

namespace RaidsRewritten.Scripts.Conditions;

public class Temperature(ILogger logger) : ISystem
{
    private readonly ILogger logger = logger;
    public record struct Component()
    {
        public float CurrentTemperature = 0.0f;
        public readonly float MaxTemp = 200.0f;
        public readonly float MinTemp = -100.0f;
        public readonly float OverheatTemp = 100.0f;
        public readonly float DeepfreezeTemp = -100.0f;
    }
    public record struct HeatChange(float Delta);
    public record struct Overheat();
    public record struct Deepfreeze();
    public record struct Id(int Value);
    public record struct TemperatureDelta(float Value, float TimeRemaining, bool SelfDestructable = false, bool Applied = false);

    /*//
    public static void HeatChangedEvent(Entity playerEntity, float delta)
    {

        using Query<Temperature.Component> q = playerEntity.CsWorld().QueryBuilder<Temperature.Component>().With(Ecs.ChildOf, playerEntity).Build();
        
        if (!q.IsTrue())
        {
            playerEntity.CsWorld().Entity("TemperatureEntity")
                .Set(new Temperature.Component())
                .Set(new Condition.Component("0", 9999.0f))
                .Set(new HeatChange(delta))
                .ChildOf(playerEntity);
        }
        else
        {
            playerEntity.Lookup("TemperatureEntity")
                .Set(new HeatChange(delta));
        }
    }
    //*/

    public static void HeatChangedEvent(Entity playerEntity, float delta, float duration = 0, int id = 0)
    {
        var world = playerEntity.CsWorld();
        using Query<Temperature.Component> q = playerEntity.CsWorld().QueryBuilder<Temperature.Component>().With(Ecs.ChildOf, playerEntity).Build();
        Entity te = default;
        if (!q.IsTrue()) {
            te = playerEntity.CsWorld().Entity()
                .Set(new Temperature.Component())
                .ChildOf(playerEntity);
        }
        else
        {
            q.Each((Entity e, ref Temperature.Component tc) => {
                te = e;
            });
        }
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
                    if (td.TimeRemaining <= 0)
                    {
                        td.TimeRemaining = duration;
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
                        logger.Info(temperature.CurrentTemperature.ToString());
                    }

                    if (delta.SelfDestructable)
                    {
                        var e = it.Entity(i);
                        e.Destruct();
                    }

                    delta.TimeRemaining = Math.Max(delta.TimeRemaining - it.DeltaTime(), 0);
                    if (delta.TimeRemaining < 0)
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

                try
                {
                    //This is constantly refreshing and creating new components, gonna work on handling the UI part to fix this in a bit
                    e.Set(new Condition.Component(Temperature.CurrentTemperature.ToString(), 9999.0f));

                    if (Temperature.CurrentTemperature <= Temperature.DeepfreezeTemp)
                    {
                        if (!e.Has<Deepfreeze>())
                        {
                            e.Remove<Overheat>();
                            e.Add<Deepfreeze>();
                            //logger.Info("Deepfreeze");
                        }
                        return;
                    }
                    if (Temperature.CurrentTemperature >= Temperature.OverheatTemp)
                    {
                        if (!e.Has<Overheat>())
                        { 
                            e.Remove<Deepfreeze>();
                            e.Add<Overheat>();
                            //logger.Info("Overheat");
                        }
                        return;
                    }
                    e.Remove<Deepfreeze>();
                    e.Remove<Overheat>();
                }
                catch (Exception ex)
                {
                    logger.Error(ex.ToStringFull());
                }
            });

        
        /*
        world.System<TemperatureDelta>()
            .Each((Iter it, int i, ref TemperatureDelta delta) =>
        {
            try
            {
                Entity TemperatureEntity = it.Entity(i).Parent();
                Component c = TemperatureEntity.Get<Component>();
                if (!delta.Applied)
                { 
                    
                }
                if (it.Entity(i).Has<Id>())  //Bouncer
                {

                } 
                if (delta.SelfDestructable)
                {
                    var e = it.Entity(i);
                    e.Destruct();
                }
                delta.TimeRemaining = Math.Max(delta.TimeRemaining - it.DeltaTime(), 0);
                if (delta.TimeRemaining <= 0)
                {
                    delta.SelfDestructable = true;
                }
            }
            catch (Exception e)
            {
                logger.Error(e.ToStringFull());
            }
        });
        //*/
       
        /*
        world.System<Component, HeatChange>()
            .Each((Iter it, int i, ref Component component, ref HeatChange heatChange) =>
            {
                var e = it.Entity(i);
                if (!e.Parent().Has<Player.Component>()) { return; }
                if (!e.Parent().Get<Player.Component>().IsLocalPlayer) { return; }

                component.CurrentTemperature += heatChange.Delta;
                component.CurrentTemperature = Math.Clamp(component.CurrentTemperature, component.MinTemp, component.MaxTemp);
                e.Remove<HeatChange>();
                //logger.Info($"{component.CurrentTemperature}");

                e.Set(new Condition.Component(component.CurrentTemperature.ToString(), 9999.0f));
               
                if (component.CurrentTemperature <= component.MinTemp)
                {
                    //logger.Info("Deepfreeze");

                    using var FreezeQuery = world.Query<Condition.Component, DeepFreeze.Component>();
                    if (FreezeQuery.IsTrue()) { return; }

                    e.Children(c =>
                    {
                        if (c.Has<Overheat.Component>())
                        {
                            c.Destruct();
                        }
                    });

                    DeepFreeze.ApplyToPlayer(e, 10.0f);

                    return;
                }
                if (component.CurrentTemperature >= component.OverHeatTemp)
                {
                    //logger.Info("Overheating");

                    using var HeatQuery = world.Query<Condition.Component, Overheat.Component>();
                    if (HeatQuery.IsTrue()) { return; }

                    e.Children(c =>
                    {
                        if (!c.Has<DeepFreeze.Component>())
                        {
                            c.Destruct();
                        }
                    });

                    Overheat.ApplyToPlayer(e, 10.0f);

                    return;
                }

                e.Children(c => {
                    if (c.Has<DeepFreeze.Component>() || c.Has<Overheat.Component>())
                    {
                        c.Destruct();
                    }
                });
            });//*/
    }
}