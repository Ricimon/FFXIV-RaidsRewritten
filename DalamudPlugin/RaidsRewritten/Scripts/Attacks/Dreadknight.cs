using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static RaidsRewritten.Scripts.Attacks.Dreadknight;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class Dreadknight(DalamudServices dalamud) : IAttack, IDisposable, ISystem
{
    public record struct Component(float ElapsedTime, float NextRefresh, float StartEnrage = 7f, float Enrage = 12f, bool EnrageLoop = false, bool BackupActive = false);
    public record struct Target(IGameObject? Value);
    public record struct BackupTarget(IGameObject? Value);
    public record struct Speed(float Value);
    public record struct AnimationState(ushort Value, bool Interrupt = false);

    private const ushort WalkingAnimation = 41;
    private const ushort AttackAnimation = 1515;
    private const ushort CastingAnimation = 1516;
    private const string SpeedBuffVfx = "vfx/monster/common/eff/mon_abh001c0c.avfx";
    private const string SpeedDebuffVfx = "vfx/common/eff/dk05th_stdn0t.avfx";
    private const string EnrageVfx1 = "vfx/monster/m0150/eff/m150sp003c1m.avfx";
    private const string EnrageVfx2 = "vfx/monster/m0150/eff/m150sp003c0m.avfx";
    private const string CastingVfx = "vfx/common/eff/mon_eisyo03t.avfx";
    private const string TetherVfx = "vfx/channeling/eff/chn_dark001f.avfx";
    private const string InterruptVfx = "vfx/common/eff/ctstop_mgc0c.avfx";

    private const float HitboxRadius = 1.75f;
    private const float StunDuration = 8f;
    private const int StunId = 0xDEAD;
    private const float StunDelay = 0.4f;
    private const int EnrageNotificationDuration = 4;
    private const float EnrageStunDuration = 60f;
    private const float EnrageStunDelay = 0.5f;
    private const float EnrageVfxDelay = 0.5f;
    private const float InitialDelay = 2f;
    private const float CastingAnimationDelay = 1.5f;

    private Query<Player.Component> playerQuery;

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
                .Set(new AnimationState(0))
                .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.QueryForLocalPlayer(world);

        // need to process AnimationState first before it's overwritten by default standing state in later system
        world.System<Model, AnimationState>()
            .Each((Iter it, int i, ref Model model, ref AnimationState animationState) =>
            {
                // set animation
                unsafe
                {
                    var clientObjectManager = ClientObjectManager.Instance();
                    if (clientObjectManager == null) { return; }

                    var obj = clientObjectManager->GetObjectByIndex((ushort)model.GameObjectIndex);
                    var chara = (Character*)obj;
                    if (chara != null)
                    {
                        chara->Timeline.BaseOverride = animationState.Value;
                        if (animationState.Interrupt) { chara->Timeline.TimelineSequencer.PlayTimeline(animationState.Value); }
                    }
                }
                // only interrupt once
                if (animationState.Interrupt)
                {
                    it.Entity(i).Set(new AnimationState(animationState.Value));
                }
            });

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

        world.System<Component, BackupTarget>()
            .Each((Iter it, int i, ref Component component, ref BackupTarget backupTarget) =>
            {
                var gameObj = backupTarget.Value;
                if (gameObj == null || !gameObj.IsValid() || gameObj.IsDead)
                {
                    it.Entity(i).Destruct();
                } 
            });

        // no target
        world.System<Component, AnimationState>().Without<Target>()
            .Each((Iter it, int i, ref Component component, ref AnimationState animationState) =>
            {
                var entity = it.Entity(i);

                if (!component.BackupActive && entity.Has<BackupTarget>())
                {
                    var backupTarget = entity.Get<BackupTarget>();
                    if (backupTarget.Value != null && backupTarget.Value.IsValid() && !backupTarget.Value.IsDead)
                    {
                        var tether = AddActorVfx(entity, TetherVfx);
                        tether.Set(new ActorVfxTarget(backupTarget.Value));
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
                    AddActorVfx(entity, CastingVfx);
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
                        ShowTextGimmick("Given uninterrupted power, the Dreadknight flies into a rage!", EnrageNotificationDuration);
                    }
                    if (animationState.Value != CastingAnimation) { entity.Set(new AnimationState(CastingAnimation, true)); }
                    DelayedAction.Create(world, () => AddActorVfx(entity, EnrageVfx1), EnrageVfxDelay);
                    DelayedAction.Create(world, () => AddActorVfx(entity, EnrageVfx2), EnrageVfxDelay);
                    StunPlayer(world, EnrageStunDuration, EnrageStunDelay);
                    component.Enrage = -1;
                    component.EnrageLoop = true;
                }
            });

        world.System<Component, Position, Rotation, Speed, AnimationState, Target>()
            .Each((Iter it, int i, ref Component component, ref Position position, ref Rotation rotation, ref Speed speed, ref AnimationState animationState, ref Target target) =>
            {
                var entity = it.Entity(i);

                if (target.Value != null && target.Value.IsValid() && !target.Value.IsDead)
                {
                    component.BackupActive = false;
                    if (component.ElapsedTime < InitialDelay) { return; }  // only want to start looking at player and chasing when ready 
                    if (component.Enrage == -1) { return; }

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
                        if (animationState.Value != AttackAnimation) { entity.Set(new AnimationState(AttackAnimation)); }
                        if (dalamud.ClientState.LocalPlayer == target.Value)
                        {
                            StunPlayer(world, StunDuration);
                        }
                        component.NextRefresh = component.ElapsedTime + 3f;
                    } else
                    {
                        // follow
                        if (animationState.Value != WalkingAnimation)
                        {
                            entity.Set(new AnimationState(WalkingAnimation, true));
                        }

                        var newPosition = position.Value;
                        newPosition.Z += speed.Value * it.DeltaTime() * MathF.Cos(angle);
                        newPosition.X += speed.Value * it.DeltaTime() * MathF.Sin(angle);
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
        if (entity.Has<Component>())
        {
            var component = entity.Get<Component>();

            if (component.Enrage == -1) { return; }

            RemoveChildren(entity);

            if (component.StartEnrage == -1)
            {
                AddActorVfx(entity, InterruptVfx);
            }
        }

        entity.Set(new Target(target));
        AddActorVfx(entity, TetherVfx)
            .Set(new ActorVfxTarget(target));
    }

    public static void RemoveTarget(Entity entity, AnimationState animationState)
    {
        RemoveChildren(entity);
        Stand(entity, animationState);
        entity.Remove<Target>();
    }

    public static void SetSpeed(Entity entity, float speed)
    {
        if (entity.Has<Speed>() && entity.Has<Component>())
        {
            var oldSpeed = entity.Get<Speed>();
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

            var oldComponent = entity.Get<Component>();
            entity.Set(new Component(oldComponent.ElapsedTime, oldComponent.ElapsedTime + CastingAnimationDelay, oldComponent.StartEnrage, oldComponent.Enrage))
                .Set(new AnimationState(CastingAnimation, true))
                .Set(new Speed(speed));
        }
    }

    public static void IncrementSpeed(Entity entity, float increment)
    {
        if (entity.Has<Speed>() && entity.Has<Component>())
        {
            var oldSpeed = entity.Get<Speed>();
            AddActorVfx(entity, SpeedBuffVfx);

            var oldComponent = entity.Get<Component>();
            entity.Set(new Component(oldComponent.ElapsedTime, oldComponent.ElapsedTime + CastingAnimationDelay, oldComponent.StartEnrage, oldComponent.Enrage))
                .Set(new AnimationState(CastingAnimation, true))
                .Set(new Speed(oldSpeed.Value + increment));
        }
    }

    public static void DecrementSpeed(Entity entity, float decrement)
    {
        if (entity.Has<Speed>() && entity.Has<Component>())
        {
            var oldSpeed = entity.Get<Speed>();
            AddActorVfx(entity, SpeedDebuffVfx);
            var newSpeed = oldSpeed.Value - decrement;
            if (newSpeed < 0) newSpeed = 0;
            
            var oldComponent = entity.Get<Component>();
            entity.Set(new Component(oldComponent.ElapsedTime, oldComponent.ElapsedTime + CastingAnimationDelay, oldComponent.StartEnrage, oldComponent.Enrage))
                .Set(new AnimationState(CastingAnimation, true))
                .Set(new Speed(newSpeed));
        }
    }

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

    private void StunPlayer(World world, float duration, float delay = StunDelay)
    {
        this.playerQuery.Each((Entity e, ref Player.Component _) =>
        {
            DelayedAction.Create(world, () => {
                Stun.ApplyToPlayer(e, duration, StunId);
            }, delay);
        });
    }

    private static void Stand(Entity entity, AnimationState animationState)
    {
        if (animationState.Value != 0) { entity.Set(new AnimationState(0)); }
    }

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .ChildOf(entity);
    }
}
