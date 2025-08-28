using System;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Log;

namespace RaidsRewritten.Scripts.Conditions;

public class Condition(ILogger logger) : ISystem
{
    public record struct Component(string Name, float TimeRemaining);
    public struct Hidden;
    public struct IgnoreOnDeath;
    private record struct Id(int Value);

    /// <summary>
    /// This method is used to refresh the duration of an existing condition with the same ID.
    /// An ID of 0 is treated as an unrefreshable condition.
    /// </summary>
    public static Entity ApplyToPlayer(Entity playerEntity, string name, float duration, int id = 0, bool extendDuration = false)
    {
        var world = playerEntity.CsWorld();
        if (id != 0)
        {
            Entity existingCondition = default;
            using var q = world.QueryBuilder<Component, Id>().With(Ecs.ChildOf, playerEntity).Build();
            q.Each((Entity e, ref Component component, ref Id idx) =>
            {
                if (idx.Value == id)
                {
                    if (extendDuration)
                    {
                        component.TimeRemaining += duration;
                    }
                    else
                    {
                        component.TimeRemaining = duration;
                    }
                    existingCondition = e;
                }
            });

            if (existingCondition.IsValid())
            {
                return existingCondition;
            }
            else
            {
                return world.Entity()
                    .Set(new Component(name, duration))
                    .Set(new Id(id))
                    .ChildOf(playerEntity);
            }
        }

        return world.Entity()
            .Set(new Component(name, duration))
            .ChildOf(playerEntity);
    }

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
                }
                catch (Exception e)
                {
                    logger.Error(e.ToStringFull());
                }
            });
    }
}