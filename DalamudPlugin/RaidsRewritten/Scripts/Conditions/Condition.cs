using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using System;
using System.Numerics;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class Condition(ILogger logger) : ISystem
{
    public record struct Component(string Name, float TimeRemaining);

    private readonly ILogger logger = logger;
    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component condition) =>
            {
                try
                {
                    condition.TimeRemaining = Math.Max(condition.TimeRemaining - it.DeltaTime(), 0);

                    if (condition.TimeRemaining <= 0)
                    {
                        var e = it.Entity(i);
                        e.Destruct();
                    }
                } catch (Exception e)
                {
                    this.logger.Error(e.ToStringFull());
                }
            });
    }

}