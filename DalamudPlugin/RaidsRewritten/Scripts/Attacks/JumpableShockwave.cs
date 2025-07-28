using System;
using System.Numerics;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public sealed class JumpableShockwave(DalamudServices dalamud, ILogger logger) : IAttack, ISystem, IDisposable
{
    public enum HitDetectionState
    {
        None,
        PlayerInside,
        PlayerOutside,
    }
    public record struct Component(float CurrentRadius, HitDetectionState HitDetectionState = HitDetectionState.None);

    private const float StartingRadius = 1.5f;
    private const float MaxRadius = 30.0f;
    private const float Speed = 4.0f;
    private const float JumpClearance = 1.0f;
    private const float StunDuration = 10.0f;
    private const int StunId = 42140;

    private Query<Player.Component> playerQuery;

    public Entity Create(World world)
    {
        var entity = world.Entity()
            .Set(new Position())
            .Set(new Rotation())
            .Set(new Scale())
            .Set(new Component(StartingRadius))
            .Add<Attack>();

        for (var i = 0; i < 2; i++)
        {
            FakeActor.Create(world)
                .Set(new ActorVfx("vfx/monster/gimmick3/eff/n4r2_b1_g4c0w.avfx"))
                .ChildOf(entity);
        }

        return entity;
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.Query(world);

        world.System<Component, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                var entity = it.Entity(i);
                var p = position.Value;
                var r = rotation.Value;

                var childCount = 0;
                entity.Children(child =>
                {
                    childCount++;

                    if (child.Has<ActorVfx>())
                    {
                        child.Set(new Position(p - 4.7f * Vector3.UnitY));

                        if (childCount == 1)
                        {
                            child.Set(new Rotation(r));
                        }
                        else
                        {
                            child.Set(new Rotation(MathUtilities.ClampRadians(r + MathF.PI)));
                        }
                    }
                });

                if (childCount == 0)
                {
                    entity.Destruct();
                    return;
                }

                var radius = component.CurrentRadius + Speed * it.DeltaTime();
                if (radius > MaxRadius)
                {
                    radius = MaxRadius;
                    return;
                }
                component.CurrentRadius = radius;

                var player = dalamud.ClientState.LocalPlayer;
                if (player == null || player.IsDead) { return; }

                var distToPlayer = Vector2.DistanceSquared(p.ToVector2(), player.Position.ToVector2());
                var newHitState = distToPlayer < radius * radius ?
                    HitDetectionState.PlayerInside :
                    HitDetectionState.PlayerOutside;

                if (component.HitDetectionState != newHitState)
                {
                    if (component.HitDetectionState != HitDetectionState.None &&
                        player.Position.Y - p.Y <= JumpClearance)
                    {
                        this.playerQuery.Each((Entity e, ref Player.Component pc) =>
                        {
                            Stun.ApplyToPlayer(e, StunDuration, StunId);
                        });
                    }
                    component.HitDetectionState = newHitState;
                }

                // Visualization
                //var e = CircleOmen.CreateEntity(it.World())
                //    .Set(new Position(p - radius * Vector3.UnitX));
                //logger.Info(radius.ToString());
                //DelayedAction.Create(world, () =>
                //{
                //    e.Destruct();
                //}, 0.1f);
            });
    }
}
