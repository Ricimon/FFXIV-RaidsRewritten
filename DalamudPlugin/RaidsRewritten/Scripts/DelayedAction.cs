using System;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts;

public class DelayedAction(ILogger logger) : ISystem
{
    public delegate void IterDelegate(ref Iter it);

    public record struct Component(Action Action, float TimeRemaining);
    public record struct IterComponent(IterDelegate Action, float TimeRemaining);

    /// <param name="delay">In seconds</param>
    public static Entity Create(World world, Action action, float delay)
    {
        return world.Entity()
            .Set(new Component(action, delay));
    }

    /// <param name="delay">In seconds</param>
    public static Entity Create(World world, IterDelegate action, float delay)
    {
        return world.Entity()
            .Set(new IterComponent(action, delay));
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                component.TimeRemaining = Math.Max(component.TimeRemaining - it.DeltaTime(), 0);
                if (component.TimeRemaining > 0) { return; }

                try
                {
                    component.Action();
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }

                it.Entity(i).Destruct();
            });

        world.System<IterComponent>()
            .Each((Iter it, int i, ref IterComponent component) =>
            {
                component.TimeRemaining = Math.Max(component.TimeRemaining - it.DeltaTime(), 0);
                if (component.TimeRemaining > 0) { return; }

                try
                {
                    component.Action.Invoke(ref it);
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }

                it.Entity(i).Destruct();
            });
    }
}
