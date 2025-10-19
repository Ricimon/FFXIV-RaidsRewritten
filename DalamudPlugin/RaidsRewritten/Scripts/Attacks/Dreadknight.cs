﻿using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class Dreadknight(DalamudServices dalamud, CommonQueries commonQueries) : IEntity, ISystem
{
    public record struct Component(float ElapsedTime, float NextRefresh, float StartEnrage = 7f, float Enrage = 12f, bool EnrageLoop = false, bool BackupActive = false);
    public record struct Target(IGameObject? Value);
    public record struct BackupTarget(IGameObject? Value);
    public record struct Speed(float Value);
    public record struct SpeedModifier(float Value);

    public struct TetherVfxChild;
    public struct CastingVfxChild;

    private const ushort WalkingAnimation = 41;
    private const ushort AttackAnimation = 1515;
    private const ushort CastingAnimation = 1516;
    private const string SpeedBuffVfx = "vfx/monster/common/eff/mon_abh001c0c.avfx";
    private const string SpeedDebuffVfx = "vfx/common/eff/dk05th_stdn0t.avfx";
    private const string EnrageVfx1 = "vfx/monster/m0150/eff/m150sp003c1m.avfx";
    private const string EnrageVfx2 = "vfx/monster/m0150/eff/m150sp003c0m.avfx";
    private const string CastingVfx = "vfx/common/eff/mon_eisyo03t.avfx";
    private const string TetherVfx = "vfx/channeling/eff/chn_dark001f.avfx";
    private const string BackupTetherVfx = "vfx/channeling/eff/chn_light01f.avfx";
    private const string InterruptVfx = "vfx/common/eff/ctstop_mgc0c.avfx";

    private const float HitboxRadius = 1.75f;
    private const float StunDuration = 8f;
    private const float StunDelay = 0.4f;
    private const int EnrageNotificationDuration = 4;
    private const float EnrageStunDuration = 60f;
    private const float EnrageStunDelay = 0.5f;
    private const float EnrageVfxDelay = 0.5f;
    private const float InitialDelay = 2f;
    private const float CastingAnimationDelay = 1.5f;
    private const string EnrageMessage = "Given uninterrupted power, the Dreadknight flies into a rage!";

    public Entity Create(World world)
    {
        return world.Entity()
                .Set(new Model(379))
                .Set(new Rotation(0))
                .Set(new Scale())
                .Set(new UniformScale(1f))
                .Set(new Component(0, 0))
                .Set(new Position())
                .Set(new Speed(3))
                .Set(new TimelineBase(0))
                .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component>()
            .Each((Iter it, int i, ref Component component) =>
            {
                component.ElapsedTime += it.DeltaTime();

                // force destruct after 5 mins
                if (component.ElapsedTime > 300)
                {
                    it.Entity(i).Destruct();
                }
            });

        // no target
        world.System<Component, TimelineBase>().Without<Target>()
            .Each((Iter it, int i, ref Component component, ref TimelineBase animationState) =>
            {
                var entity = it.Entity(i);

                if (!component.BackupActive && entity.TryGet<BackupTarget>(out var backupTarget))
                {
                    if (backupTarget.Value != null && backupTarget.Value.IsValid() && !backupTarget.Value.IsDead)
                    {
                        AddActorVfx(entity, BackupTetherVfx)
                            .Set(new ActorVfxTarget(backupTarget.Value))
                            .Add<TetherVfxChild>();
                        component.BackupActive = true;
                    }
                }

                if (component.ElapsedTime < component.StartEnrage) { return; }
                if (component.ElapsedTime < component.Enrage)
                {
                    // already started preparing enrage
                    if (component.StartEnrage == -1) {
                        Stand(entity, animationState);
                        return;
                    }

                    // start casting enrage
                    AddActorVfx(entity, CastingVfx)
                        .Add<CastingVfxChild>();
                    component.StartEnrage = -1;
                } else
                {
                    if (component.Enrage == -1)
                    {  // already enraged
                        Stand(entity, animationState);
                        component.Enrage = component.ElapsedTime + 5;
                        return;
                    }

                    // enrage
                    RemoveChildren(entity);
                    if (!component.EnrageLoop)
                    {
                        ShowTextGimmick(EnrageMessage, EnrageNotificationDuration);
                        dalamud.ChatGui.Print(EnrageMessage);
                    }
                    if (animationState.Value != CastingAnimation) { entity.Set(new TimelineBase(CastingAnimation, true)); }
                    DelayedAction.Create(world, () => AddActorVfx(entity, EnrageVfx1), EnrageVfxDelay).ChildOf(entity);
                    DelayedAction.Create(world, () => AddActorVfx(entity, EnrageVfx2), EnrageVfxDelay).ChildOf(entity);
                    StunPlayer(entity, EnrageStunDuration, EnrageStunDelay);
                    component.Enrage = -1;
                    component.EnrageLoop = true;
                }
            });

        world.System<Component, Position, Rotation, Speed, TimelineBase, Target>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Speed speed, ref TimelineBase animationState, ref Target target) =>
            {
                var entity = it.Entity(i);

                if (target.Value != null && target.Value.IsValid() && !target.Value.IsDead)
                {
                    component.BackupActive = false;
                    if (component.ElapsedTime < InitialDelay) { return; }  // only want to start looking at player and chasing when ready 
                    if (component.Enrage == -1) { return; }
                    if (entity.HasChild<Stun.Component>() || entity.HasChild<Bind.Component>() || entity.HasChild<Sleep.Component>())
                    {
                        Stand(entity, animationState);
                        return;
                    }

                    component.StartEnrage = component.ElapsedTime + 5;
                    component.Enrage = component.ElapsedTime + 10;

                    // face player
                    var sourcePosV2 = new Vector2(position.Value.X, position.Value.Z);
                    var targetPosV2 = new Vector2(target.Value.Position.X, target.Value.Position.Z);
                    var angle = MathUtilities.GetAbsoluteAngleFromSourceToTarget(sourcePosV2, targetPosV2);
                    rotation.Value = angle;

                    if (component.ElapsedTime < component.NextRefresh) {
                        Stand(entity, animationState);
                        return;
                    }

                    if (Vector2.Distance(sourcePosV2, targetPosV2) < HitboxRadius)
                    {
                        // attack
                        if (animationState.Value != AttackAnimation) { entity.Set(new TimelineBase(AttackAnimation)); }
                        if (dalamud.ClientState.LocalPlayer == target.Value)
                        {
                            StunPlayer(entity, StunDuration);
                            component.NextRefresh = component.ElapsedTime + 3f;
                        }
                    } else
                    {
                        // follow
                        if (animationState.Value != WalkingAnimation)
                        {
                            entity.Set(new TimelineBase(WalkingAnimation, true));
                        }
                        var velocity = speed.Value;

                        if (entity.HasChild<Heavy.Component>() && entity.TryGet<SpeedModifier>(out var modifier))
                        {
                            velocity *= modifier.Value;
                        }

                        var newPosition = position.Value;
                        newPosition.Z += velocity * it.DeltaTime() * MathF.Cos(angle);
                        newPosition.X += velocity * it.DeltaTime() * MathF.Sin(angle);
                        position.Value = newPosition;
                    }
                } else
                {
                    RemoveTarget(entity, animationState);
                }
            });
    }

    public static bool HasTarget(Entity entity) => entity.Has<Target>();

    public static void ApplyTarget(Entity entity, IGameObject target)
    {
        entity.CsWorld().Defer(() => {
            if (entity.TryGet<Component>(out var component))
            {
                if (component.Enrage == -1) { return; }

                entity.DestructChildEntity<TetherVfxChild>();
                entity.DestructChildEntity<CastingVfxChild>();

                if (component.StartEnrage == -1)
                {
                    AddActorVfx(entity, InterruptVfx);
                }
            }

            entity.Set(new Target(target));
            AddActorVfx(entity, TetherVfx)
                .Set(new ActorVfxTarget(target))
                .Add<TetherVfxChild>();
        });
    }

    public static void RemoveTarget(Entity entity, TimelineBase animationState)
    {
        entity.CsWorld().Defer(() =>
        {
            entity.DestructChildEntity<TetherVfxChild>();
            Stand(entity, animationState);
            entity.Remove<Target>();
        });
    }

    public static void ChangeTetherVfx(Entity entity, string VfxPath = TetherVfx)
    {
        entity.CsWorld().Defer(() =>
        {
            if (entity.TryGet<Target>(out var target))
            {
                entity.DestructChildEntity<TetherVfxChild>();

                AddActorVfx(entity, VfxPath)
                    .Set(new ActorVfxTarget(target.Value))
                    .Add<TetherVfxChild>();
            }
        });
    }

    public static void SetSpeed(Entity entity, float speed)
    {
        if (entity.TryGet<Speed>(out var oldSpeed) && entity.TryGet<Component>(out var oldComponent))
        {
            if (oldSpeed.Value > speed)
            {
                AddActorVfx(entity, SpeedDebuffVfx);
            } else if (oldSpeed.Value < speed)
            {
                AddActorVfx(entity, SpeedBuffVfx);
            } else
            {
                return;
            }

            entity.Set(new Component(oldComponent.ElapsedTime, oldComponent.ElapsedTime + CastingAnimationDelay, oldComponent.StartEnrage, oldComponent.Enrage))
                .Set(new TimelineBase(CastingAnimation, true))
                .Set(new Speed(speed));
        }
    }

    public static void IncrementSpeed(Entity entity, float increment)
    {
        if (entity.TryGet<Speed>(out var oldSpeed) && entity.TryGet<Component>(out var oldComponent))
        {
            AddActorVfx(entity, SpeedBuffVfx);

            entity.Set(new Component(oldComponent.ElapsedTime, oldComponent.ElapsedTime + CastingAnimationDelay, oldComponent.StartEnrage, oldComponent.Enrage))
                .Set(new TimelineBase(CastingAnimation, true))
                .Set(new Speed(oldSpeed.Value + increment));
        }
    }

    public static void DecrementSpeed(Entity entity, float decrement)
    {
        if (entity.TryGet<Speed>(out var oldSpeed) && entity.TryGet<Component>(out var oldComponent))
        {
            AddActorVfx(entity, SpeedDebuffVfx);
            var newSpeed = oldSpeed.Value - decrement;
            if (newSpeed < 0) newSpeed = 0;
            
            entity.Set(new Component(oldComponent.ElapsedTime, oldComponent.ElapsedTime + CastingAnimationDelay, oldComponent.StartEnrage, oldComponent.Enrage))
                .Set(new TimelineBase(CastingAnimation, true))
                .Set(new Speed(newSpeed));
        }
    }

    public static void SetTemporaryRelativeSpeed(Entity entity, float value) => entity.Set(new SpeedModifier(value));

    // maybe move this to a util class?
    private void ShowTextGimmick(string text, int seconds, RaptureAtkModule.TextGimmickHintStyle style = RaptureAtkModule.TextGimmickHintStyle.Warning)
    {
        unsafe
        {
            var raptureAtkModule = RaptureAtkModule.Instance();
            if (raptureAtkModule == null) { return; }

            raptureAtkModule->ShowTextGimmickHint(
            text,
            style,
            10 * seconds);
        }
    }

    public static void RemoveChildren(Entity entity)
    {
        entity.CsWorld().DeleteWith(flecs.EcsChildOf, entity);
    }

    // can't directly remove components, instead schedule removal from a system
    public static void RemoveCancellableCC(Entity entity) => entity.CsWorld().Defer(() =>
    {
        entity.DestructChildEntity<Sleep.Component>();
        entity.DestructChildEntity<Bind.Component>();
    });

    private void StunPlayer(Entity entity, float duration, float delay = StunDelay)
    {
        var player = dalamud.ClientState.LocalPlayer;
        if (player == null || player.IsDead) { return; }
        commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
        {
            DelayedAction.Create(entity.CsWorld(), () => {
                Stun.ApplyToTarget(e, duration);
            }, delay).ChildOf(entity);
        });
    }

    private static void Stand(Entity entity, TimelineBase animationState)
    {
        if (animationState.Value != 0) { entity.Set(new TimelineBase(0)); }
    }

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .ChildOf(entity);
    }
}
