using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class VoidGate() : IEntity, ISystem
{
    public enum Phase
    {
        Spawn,
        Expel,
        Reset
    }

    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Spawn, 5.5f },
        { Phase.Expel, 16.0f },
        { Phase.Reset, 20.0f }
    };

    public record struct Component(float ElapsedTime, Phase Phase = Phase.Spawn);
    public struct Vfx;
    public struct Destruct;
    public struct Spawn;
    public struct GateActor;
    public struct AnimationActor;

    private const float OmenDuration = 0.75f;
    private const ushort IdleAnimation = 34;
    private const ushort AttackAnimation = 3212;
    private const float HysteriaDuration = 30f;
    private const float RedirectInterval = 15f;
    private const string AbsorbVfx = "vfx/monster/c0101/eff/c0101wpinc0c.avfx";
    private const string ExpelVfx = "vfx/monster/c0101/eff/c0101wpouc0c.avfx";
    private const string GateActorVfx = "chara/monster/m0273/obj/body/b0001/vfx/eff/vm0001.avfx";
    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(-1))
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new Component())
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);

    public void Register(World world)
    {
        world.System<Component, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                component.ElapsedTime += it.DeltaTime();

                Entity animationActor;
                Entity gateActor;

                var entity = it.Entity(i);

                using var animationQuery = world.QueryBuilder().With(flecs.EcsChildOf, entity).With<AnimationActor>().Build();
                if (!animationQuery.IsTrue())
                {
                    animationActor = FakeActor.Create(it.World())
                        .Set(new Position(position.Value))
                        .Set(new Rotation(rotation.Value))
                        .Add<AnimationActor>()
                        .ChildOf(entity);
                }
                else
                {
                    animationActor = animationQuery.First();
                }

                using var gateQuery = world.QueryBuilder().With(flecs.EcsChildOf, entity).With<GateActor>().Build();
                if (!gateQuery.IsTrue())
                {
                    gateActor = FakeActor.Create(it.World())
                        .Set(new Position(position.Value))
                        .Set(new Rotation(rotation.Value))
                        .Add<GateActor>()
                        .ChildOf(entity);
                }
                else
                {
                    gateActor = gateQuery.First();
                }

                switch (component.Phase)
                {
                    case Phase.Spawn:
                        if (ShouldReturn(component)) { return; }
                        animationActor.Set(new ActorVfx(AbsorbVfx));
                        DelayedAction.Create(world, () =>
                        {
                            gateActor.Set(new ActorVfx(GateActorVfx));
                        }, 0.5f);

                        component.Phase = Phase.Expel;

                        break;
                    case Phase.Expel:
                        if (ShouldReturn(component)) { return; }

                        animationActor.Set(new ActorVfx(ExpelVfx));
                        DelayedAction.Create(world, () =>
                        {
                            gateActor.Destruct();
                        }, 0.5f);
                        component.Phase = Phase.Reset;
                        break;
                    case Phase.Reset:
                        if (ShouldReturn(component)) { return; }
                        entity.Destruct();
                        break;
                }
               
                

                entity.Remove<Spawn>();
            });
        /*
        world.System<Component, Position, Rotation>().With<Vfx>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation) =>
            {

                Entity fakeactor;
                var entity = it.Entity(i);
                /*
                using var animationQuery = world.QueryBuilder().With(flecs.EcsChildOf, entity).With<FakeActor.Component, AnimationActor>().Build();
                if (animationQuery.IsTrue())
                { 
                    fakeactor = animationQuery.First();
                    fakeactor.Set(new ActorVfx(ExpelVfx));
                }

                /*
                using var gateQuery = world.QueryBuilder().With(flecs.EcsChildOf, entity).With<FakeActor.Component, GateActor>().Build();
                gateQuery.Each((Iter it, int i) =>
                {
                    var e = it.Entity(i);
                    e.Destruct();
                });
                entity.Remove<Vfx>();
            });
        */
    }

    private bool ShouldReturn(Component component) => component.ElapsedTime < phaseTimings[component.Phase];

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .ChildOf(entity);
    }
}
