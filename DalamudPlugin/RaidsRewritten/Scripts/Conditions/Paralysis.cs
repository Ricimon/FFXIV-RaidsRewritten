using System;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Paralysis(Random random, ILogger logger) : ISystem
{
    private const string IconId = "215006";

    public record struct Component(float StunInterval, float StunDuration,
        float ElapsedTime = 0, float TimeOffset = 0, bool StunActive = false, int LastTimeIntervalEvaluation = -1);

    public static void ApplyToTarget(
        Entity target,
        float duration,
        float stunInterval,
        float stunDuration,
        bool extendDuration = false)
    {
        ApplyToTarget(target, duration, stunInterval, stunDuration, ConditionTable.Id.Paralysis, extendDuration);
    }

    public static void ApplyToTarget(
        Entity target,
        float duration,
        float stunInterval,
        float stunDuration,
        BigInteger id,
        bool extendDuration = false,
        bool overrideExistingDuration = false,
        bool isClientControlled = true)
    {
        DelayedAction.Create(target.CsWorld(), (ref Iter it) =>
        {
            var world = it.World();

            var condition = Condition.ApplyToTarget(target, "Paralyzed", duration, id, extendDuration, overrideExistingDuration, isClientControlled);
            if (!condition.Has<Component>())
            {
                condition.Set(new Component(stunInterval, stunDuration, TimeOffset: stunInterval));
            }

            condition
                .Set(new Condition.NetworkMessage(Network.Message.Condition.Paralysis))
                .Set(new Condition.StatusIconReplacement(IconId, ConditionTable.IconToReplace.Paralysis))
                .Set(new Condition.Status(ConditionTable.IconToReplace.Paralysis, "Paralysis", "Deadened nerves are sometimes preventing the execution of actions."))
                .Set(new Condition.StatusTooltip("Paralysis (RaidsRewritten)"))
                .Add<Condition.StatusEnfeeblement>();

        }, 0, true).ChildOf(target);
    }

    public void Register(World world)
    {
        world.System<Player.Component, Component>()
            .TermAt(0).Up()
            .With<Player.LocalPlayer>().Up()
            .Each((Iter it, int i, ref Player.Component pc, ref Component component) =>
            {
                component.ElapsedTime += it.DeltaTime();

                if (component.StunInterval == 0 || component.StunDuration == 0)
                {
                    component.StunActive = false;
                    return;
                }

                var elapsedTime = component.ElapsedTime + component.TimeOffset;
                var interval = component.StunInterval + component.StunDuration;
                var divT = (int)(elapsedTime / interval);
                var modT = elapsedTime % interval;

                if (modT <= component.StunInterval)
                {
                    component.StunActive = false;
                    return;
                }

                if (divT != component.LastTimeIntervalEvaluation)
                {
                    component.LastTimeIntervalEvaluation = divT;
                    var stun = random.Next(10) > 1; // 80%
                    if (stun)
                    {
                        world.Entity()
                            .Set(new ActorVfx("vfx/common/eff/dk05ht_sta0h.avfx"))
                            .ChildOf(it.Entity(i));
                        component.StunActive = true;
                    }
                }
            });
    }
}
