using System;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts;

public class DelayedAction(ILogger logger) : ISystem
{
    public delegate void IterDelegate(ref Iter it);

    public record struct Component(Action Action, float TimeRemaining);
    public record struct IterComponent(IterDelegate Action, float TimeRemaining);
    public struct Immediate;

    /// <summary>
    /// Setting immediate to true specifies that the system that runs this action should execute in
    /// immediate mode, suspending any deferrals and modifying components/entities at the moment of execution.
    /// See https://www.flecs.dev/flecs/md_docs_2Systems.html#immediate-systems for more information
    /// </summary>
    /// <param name="delay">In seconds</param>
    public static Entity Create(World world, Action action, float delay, bool immediate = false)
    {
        var entity = world.Entity().Set(new Component(action, delay));
        if (immediate)
        {
            entity.Add<Immediate>();
        }
        return entity;
    }

    /// <summary>
    /// Setting immediate to true specifies that the system that runs this action should execute in
    /// immediate mode, suspending any deferrals and modifying components/entities at the moment of execution.
    /// See https://www.flecs.dev/flecs/md_docs_2Systems.html#immediate-systems for more information
    /// </summary>
    /// <param name="delay">In seconds</param>
    public static Entity Create(World world, IterDelegate action, float delay, bool immediate = false)
    {
        var entity = world.Entity().Set(new IterComponent(action, delay));
        if (immediate)
        {
            entity.Add<Immediate>();
        }
        return entity;
    }

    public void Register(World world)
    {
        world.System<Component>().Without<Immediate>()
            .Each((Iter it, int i, ref Component component) =>
            {
                Run(it, i, ref component, false);
            });
        world.System<Component>().With<Immediate>().Immediate()
            .Each((Iter it, int i, ref Component component) =>
            {
                Run(it, i, ref component, true);
            });

        world.System<IterComponent>().Without<Immediate>()
            .Each((Iter it, int i, ref IterComponent component) =>
            {
                Run(it, i, ref component, false);
            });
        world.System<IterComponent>().With<Immediate>().Immediate()
            .Each((Iter it, int i, ref IterComponent component) =>
            {
                Run(it, i, ref component, true);
            });
    }

    private void Run(Iter it, int i, ref Component component, bool immediate)
    {
        component.TimeRemaining = Math.Max(component.TimeRemaining - it.DeltaTime(), 0);
        if (component.TimeRemaining > 0) { return; }

        try
        {
            if (immediate) { it.World().DeferSuspend(); }
            component.Action();
            if (immediate) { it.World().DeferResume(); }
        }
        catch (Exception e)
        {
            logger.Error(e.ToStringFull());
        }

        it.Entity(i).Destruct();
    }

    private void Run(Iter it, int i, ref IterComponent component, bool immediate)
    {
        component.TimeRemaining = Math.Max(component.TimeRemaining - it.DeltaTime(), 0);
        if (component.TimeRemaining > 0) { return; }

        try
        {
            if (immediate) { it.World().DeferSuspend(); }
            component.Action.Invoke(ref it);
            if (immediate) { it.World().DeferResume(); }
        }
        catch (Exception e)
        {
            logger.Error(e.ToStringFull());
        }

        it.Entity(i).Destruct();
    }
}
