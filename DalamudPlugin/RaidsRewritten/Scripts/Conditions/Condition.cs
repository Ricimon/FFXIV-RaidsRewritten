using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Network;

namespace RaidsRewritten.Scripts.Conditions;

public class Condition(ILogger logger) : ISystem
{
    public record struct Component(string Name, float TimeRemaining, DateTime CreationTime);
    public record struct Id(BigInteger Value);
    public record struct NetworkMessage(Message.Condition Condition);
    public record struct StatusIconReplacement(string CustomIconName, int IconToReplace);
    public struct Hidden;
    public struct IgnoreOnDeath;
    public struct SyncedToServer;
    public struct ServerCondition;

    public record struct Status(int Icon, string Title, string Description, int TooltipShown = -1);
    public record struct StatusTooltip(string Title);
    public struct StatusEnhancement;
    public struct StatusEnfeeblement;
    public struct StatusOther;

    /// <summary>
    /// This method can be used to refresh the duration of an existing condition with the same ID.
    /// </summary>
    public static Entity ApplyToTarget(Entity target, string name, float duration, BigInteger id, bool extendDuration, bool overrideExistingDuration, bool isClientControlled = true)
    {
        var world = target.CsWorld();
        Entity existingCondition = default;
        using var q = world.QueryBuilder<Component, Id>().With(Ecs.ChildOf, target).Build();
        q.Each((Entity e, ref Component component, ref Id idx) =>
        {
            if (idx.Value == id)
            {
                if (extendDuration)
                {
                    component.TimeRemaining += duration;
                }
                else if (duration > component.TimeRemaining || overrideExistingDuration)
                {
                    component.TimeRemaining = duration;
                }
                existingCondition = e;
            }
        });

        Entity e;
        if (existingCondition.IsValid())
        {
            e = existingCondition;
        }
        else
        {
            e = world.Entity()
                .Set(new Component(name, duration, DateTime.UtcNow))
                .Set(new Id(id))
                .ChildOf(target);
        }

        e.Remove<SyncedToServer>();

        if (isClientControlled)
        {
            e.Remove<ServerCondition>();
        }
        else
        {
            e.Add<ServerCondition>();
        }
        return e;
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component condition) =>
            {
                var e = it.Entity(i);

                condition.TimeRemaining = Math.Max(condition.TimeRemaining - it.DeltaTime(), 0);

                if (!e.Has<ServerCondition>() && condition.TimeRemaining <= 0)
                {
                    e.Destruct();
                }
            });
    }
}