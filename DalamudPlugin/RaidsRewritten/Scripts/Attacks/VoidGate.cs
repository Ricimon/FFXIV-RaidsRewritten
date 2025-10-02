using System.Numerics;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class VoidGate() : IEntity, ISystem
{
    public enum Phase
    {
        Spawn,
        Expel,
        Reset
    }

    public record struct SpawnDelay(float DelayTime = 1.0f);
    public record struct ExpelDelay(float DelayTime = 1.0f);
    public record struct Component(float ElapsedTime, Phase Phase = Phase.Spawn);
    public struct GateActor;
    public struct AnimationActor;

    private const float ResetBuffer = 5.0f;
    private const string AbsorbVfx = "vfx/monster/c0101/eff/c0101wpinc0c.avfx";
    private const string ExpelVfx = "vfx/monster/c0101/eff/c0101wpouc0c.avfx";
    private const string GateActorVfx = "chara/monster/m0273/obj/body/b0001/vfx/eff/vm0001.avfx";
    public static Entity CreateEntity(World world)
    {
        return FakeActor.Create(world)
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new UniformScale(1f))
            .Set(new Component())
            .Set(new SpawnDelay())
            .Set(new ExpelDelay())
            .Add<Attack>();
    }

    public Entity Create(World world) => CreateEntity(world);

    public void Register(World world)
    {
        world.System<Component, Position, Rotation, SpawnDelay, ExpelDelay>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref SpawnDelay spawnDelay, ref ExpelDelay expelDelay) =>
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
                        if (component.ElapsedTime < spawnDelay.DelayTime) { return; }
                        AddActorVfx(animationActor, AbsorbVfx);
                        DelayedAction.Create(world, () =>
                        {
                            AddActorVfx(gateActor, GateActorVfx);
                        }, 0.5f);

                        component.Phase = Phase.Expel;

                        break;
                    case Phase.Expel:
                        if (component.ElapsedTime < expelDelay.DelayTime) { return; }

                        AddActorVfx(animationActor, ExpelVfx);
                        
                        DelayedAction.Create(world, () =>
                        {
                            gateActor.Destruct();
                        }, 0.5f);
                        
                        component.Phase = Phase.Reset;
                        break;
                    case Phase.Reset:
                        if (component.ElapsedTime < expelDelay.DelayTime + ResetBuffer) { return; }
                        entity.Destruct();
                        break;
                }
            });
    }

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .Set(new Scale(new Vector3(2f,2f,2f)))
            .ChildOf(entity);
    }
}
