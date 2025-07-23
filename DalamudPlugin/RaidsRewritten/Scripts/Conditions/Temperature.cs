using System;
using Flecs.NET.Core;
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
        public readonly float OverHeatTemp = 100.0f;
    }
    public record struct HeatChange(float Delta);
    public record struct TempUI;
    public static void HeatChangedEvent(Entity playerEntity, float delta)
    {
        Entity t = playerEntity.Lookup("TemperatureEntity");
        if (t == 0) { return; }
        t.Set(new HeatChange(delta));
    }

    public void Register(World world)
    {
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
            });
    }
}