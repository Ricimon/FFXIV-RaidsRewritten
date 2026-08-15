using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;

namespace RaidsRewritten.Scripts.Attacks;

public class Circle(DalamudServices dalamud, CommonQueries commonQueries, ILogger logger) : IEntity, ISystem
{
    public record struct Component(
        float OmenDuration,
        float AttackVfxDelay,
        string? AttackVfxPath,
        float OnHitDelay,
        Action<Entity> OnHit);

    private record struct Runtime(
        float ElapsedTime = 0,
        Entity Omen = default,
        bool Snapshotted = false,
        bool PlayerHit = false,
        Entity AttackVfx = default,
        bool AttackVfxPlayed = false,
        bool OnHitExecuted = false);

    public readonly DalamudServices dalamud = dalamud;
    public readonly ILogger logger = logger;

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component())
            .Set(new Runtime())
            .Add<Attack>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }

    public void Register(World world)
    {
        world.System<Component, Runtime, Position, Rotation, Scale>()
            .Each((Iter it, int i, ref Component component, ref Runtime runtime, ref Position position, ref Rotation rotation, ref Scale scale) =>
            {
                var entity = it.Entity(i);
                runtime.ElapsedTime += it.DeltaTime();

                if (runtime.ElapsedTime < component.OmenDuration)
                {
                    if (!runtime.Omen.IsValid())
                    {
                        runtime.Omen = CircleOmen.CreateEntity(it.World())
                            .Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(scale.Value))
                            .Set(new OmenDuration(component.OmenDuration - runtime.ElapsedTime, false))
                            .ChildOf(entity);
                    }
                }
                else
                {
                    if (runtime.Omen.IsValid())
                    {
                        runtime.Omen.Destruct();
                    }
                }

                if (!runtime.Snapshotted)
                {
                    if (runtime.ElapsedTime >= component.OmenDuration)
                    {
                        runtime.Snapshotted = true;
                        var player = this.dalamud.ObjectTable.LocalPlayer;

                        if (player != null && !player.IsDead)
                        {
                            var distanceToCenter = Vector2.Distance(position.Value.ToVector2(), player.Position.ToVector2());
                            var onHit = component.OnHit;

                            if (distanceToCenter <= scale.Value.Z)
                            {
                                runtime.PlayerHit = true;
                            }
                        }
                    }
                }

                if (!runtime.OnHitExecuted)
                {
                    if (runtime.ElapsedTime >= component.OmenDuration + component.OnHitDelay)
                    {
                        runtime.OnHitExecuted = true;
                        if (runtime.PlayerHit)
                        {
                            var onHit = component.OnHit;
                            commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                            {
                                onHit(e);
                            });
                        }
                    }
                }

                if (!runtime.AttackVfxPlayed)
                {
                    if (runtime.ElapsedTime >= component.OmenDuration + component.AttackVfxDelay)
                    {
                        runtime.AttackVfxPlayed = true;
                        if (!string.IsNullOrEmpty(component.AttackVfxPath))
                        {
                            runtime.AttackVfx = FakeActor.Create(it.World())
                                .Set(new Position(position.Value))
                                .Set(new Rotation(rotation.Value))
                                .Set(new ActorVfx(component.AttackVfxPath))
                                .ChildOf(entity);
                        }
                    }
                }

                if (runtime.Snapshotted && runtime.AttackVfxPlayed && runtime.OnHitExecuted && !runtime.AttackVfx.IsValid())
                {
                    logger.Info("Circle destructed");
                    entity.Destruct();
                }
            });
    }
}
