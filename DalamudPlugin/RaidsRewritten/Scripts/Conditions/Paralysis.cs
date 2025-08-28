using System;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;

namespace RaidsRewritten.Scripts.Conditions;

public class Paralysis(Random random, ILogger logger) : ISystem
{
    public record struct Component(float StunInterval, float StunDuration,
        float ElapsedTime = 0, float TimeOffset = 0, bool StunActive = false, int LastTimeIntervalEvaluation = -1);

    public static Entity ApplyToPlayer(Entity playerEntity, float duration, float stunInterval, float stunDuration, int id = 0)
    {
        var entity = Condition.ApplyToPlayer(playerEntity, "Paralyzed", duration, id);
        if (!entity.Has<Component>())
        {
            entity.Set(new Component(stunInterval, stunDuration, TimeOffset: stunInterval));
        }
        return entity;
    }

    public void Register(World world)
    {
        world.System<Player.Component, Component>()
            .TermAt(0).Up()
            .With<Player.LocalPlayer>().Up()
            .Each((Iter it, int i, ref Player.Component pc, ref Component component) =>
            {
                //if (component.ElapsedTime == 0)
                //{
                //    component.TimeOffset = random.NextSingle() * component.StunInterval;
                //}

                component.ElapsedTime += it.DeltaTime();

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
