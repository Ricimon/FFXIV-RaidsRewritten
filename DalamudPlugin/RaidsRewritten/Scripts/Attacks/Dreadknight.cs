using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
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
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class Dreadknight(DalamudServices dalamud) : IAttack, IDisposable, ISystem
{
    public record struct Component(float ElapsedTime, float NextRefresh, float StartEnrage = 7f, float Enrage = 12f);
    public record struct Target(IGameObject? Value);
    public record struct Speed(float Value);
    public record struct Child(object _);

    private const ushort WalkingAnimation = 41;
    private const ushort AttackAnimation = 1515;
    private const ushort EnrageAnimation = 1516;
    private const string SpeedBuffVfx = "vfx/eureka/erk011/eff/abi_erk011c0p.avfx";
    private const string SpeedDebuffVfx = "vfx/common/eff/dk05th_stdn0t.avfx";
    private const string EnrageVfx1 = "vfx/monster/m0150/eff/m150sp003c1m.avfx";
    private const string EnrageVfx2 = "vfx/monster/m0150/eff/m150sp003c0m.avfx";
    private const string CastingVfx = "vfx/common/eff/mon_eisyo03t.avfx";
    private const string TetherVfx = "vfx/channeling/eff/chn_tergetfix1f.avfx";

    private const float HitboxRadius = 1.75f;
    private const float StunDuration = 8f;
    private const int StunId = 0xDEAD;
    private const float StunDelay = 0.4f;
    private const int EnrageNotificationDuration = 4;
    private const float EnrageStunDuration = 60f;
    private const float EnrageStunDelay = 0.5f;
    private const float EnrageVfxDelay = 0.5f;
    private const float InitialDelay = 2f;

    private Query<Player.Component> playerQuery;

    public Entity Create(World world)
    {
        dalamud.ToastGui.ShowNormal("The Dreadknight seeks signs of resistance...");
        return world.Entity()
                .Set(new Model(379))
                .Set(new Rotation(0))
                .Set(new Scale())
                .Set(new UniformScale(1f))
                .Set(new Component(0, 0))
                .Set(new Position())
                .Set(new Speed(3))
                .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.Query(world);

        world.System<Model, Component, Position, Rotation, Speed>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Position position, ref Rotation rotation, ref Speed speed) =>
            {
                component.ElapsedTime += it.DeltaTime();
                var entity = it.Entity(i);

                if (component.ElapsedTime < InitialDelay) { return; }  // only want to start looking at player and chasing when ready 

                if (entity.TryGet<Target>(out var target))
                {
                    if (target.Value != null && (target.Value.IsValid() && !target.Value.IsDead))
                    {
                        component.StartEnrage = component.ElapsedTime + 5;
                        component.Enrage = component.ElapsedTime + 10;

                        // face player
                        var sourcePosV2 = new Vector2(position.Value.X, position.Value.Z);
                        var targetPosV2 = new Vector2(target.Value.Position.X, target.Value.Position.Z);
                        var angle = MathUtilities.GetAbsoluteAngleFromSourceToTarget(sourcePosV2, targetPosV2);
                        rotation.Value = angle;

                        if (component.ElapsedTime < component.NextRefresh) {
                            Stand(ref model);
                            return;
                        }

                        if (Vector2.Distance(sourcePosV2, targetPosV2) < HitboxRadius)
                        {
                            Hit(world, ref model, ref component, target);
                        } else
                        {
                            Follow(it, ref model, ref component, ref position, ref speed, angle);
                        }
                    } else
                    {
                        RemoveTarget(entity);
                    }
                } else
                {
                    HandleNoTarget(world, entity, ref model, ref component);
                }

                // force destruct after 5 mins
                if (component.ElapsedTime > 300)
                {
                    entity.Destruct();
                }
            });
    }

    public static bool HasTarget(Entity entity) => entity.Has<Target>();

    public static void ApplyTarget(Entity entity, IGameObject target)
    {
        RemoveChildren(entity);

        entity.Set(new Target(target));
        AddActorVfx(entity, TetherVfx)
            .Set(new ActorVfxTarget(target));
    }

    public static void RemoveTarget(Entity entity)
    {
        RemoveChildren(entity);
        if (entity.Has<Model>())
        {
            var model = entity.Get<Model>();
            Stand(ref model);
        }
        entity.Remove<Target>();
    }

    public static void SetSpeed(Entity entity, float speed)
    {
        if (entity.Has<Speed>())
        {
            var oldSpeed = entity.Get<Speed>();
            if (oldSpeed.Value > speed)
            {
                AddActorVfx(entity, SpeedDebuffVfx);
            } else if (oldSpeed.Value < speed)
            {
                AddActorVfx(entity, SpeedBuffVfx);
            }
            entity.Set(new Speed(speed));
        }
    }

    public static void IncrementSpeed(Entity entity, float increment)
    {
        if (entity.Has<Speed>())
        {
            var oldSpeed = entity.Get<Speed>();
            AddActorVfx(entity, SpeedBuffVfx);
            entity.Set(new Speed(oldSpeed.Value + increment));
        }
    }

    public static void DecrementSpeed(Entity entity, float increment)
    {
        if (entity.Has<Speed>())
        {
            var oldSpeed = entity.Get<Speed>();
            AddActorVfx(entity, SpeedDebuffVfx);
            var newSpeed = oldSpeed.Value - increment;
            if (newSpeed < 0) newSpeed = 0;
            entity.Set(new Speed(newSpeed));
        }
    }

    private static void SetTimeline(Model model, ushort animationId)
    {
        unsafe
        {
            var clientObjectManager = ClientObjectManager.Instance();
            if (clientObjectManager == null) { return; }

            var obj = clientObjectManager->GetObjectByIndex((ushort)model.GameObjectIndex);
            var chara = (Character*)obj;
            if (chara != null)
            {
                chara->Timeline.BaseOverride = animationId;
            }
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

    // does not work with multiple dreadknights
    public static void RemoveChildren(Entity entity)
    {
        entity.CsWorld().DeleteWith<Child>();
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

    private static void Stand(ref Model model)
    {
        SetTimeline(model, 0);
    }

    private void Follow(Iter it, ref Model model, ref Component component, ref Position position, ref Speed speed, float angle)
    {
        SetTimeline(model, WalkingAnimation);
        var newPosition = position.Value;
        newPosition.Z += speed.Value * it.DeltaTime() * MathF.Cos(angle);
        newPosition.X += speed.Value * it.DeltaTime() * MathF.Sin(angle);
        position.Value = newPosition;
    }

    private void Hit(World world, ref Model model, ref Component component, Target target)
    {
        SetTimeline(model, AttackAnimation);
        if (dalamud.ClientState.LocalPlayer == target.Value)
        {
            StunPlayer(world, StunDuration);
        }
        component.NextRefresh = component.ElapsedTime + 3f;
    }

    private static void CastEnrage(Entity entity, ref Component component)
    {
        AddActorVfx(entity, CastingVfx);
        component.StartEnrage = -1;
    }

    private void Enrage(World world, Entity entity, ref Model model, ref Component component)
    {
        RemoveChildren(entity);
        ShowTextGimmick("Enraged without the sight of resistance, the Dreadknight lets out a deafening shrill!", EnrageNotificationDuration);
        SetTimeline(model, EnrageAnimation);
        DelayedAction.Create(world, () => AddActorVfx(entity, EnrageVfx1), EnrageVfxDelay);
        DelayedAction.Create(world, () => AddActorVfx(entity, EnrageVfx2), EnrageVfxDelay);
        StunPlayer(world, EnrageStunDuration, EnrageStunDelay);
        component.Enrage = -1;
    }

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .Set(new Child())
            .ChildOf(entity);
    }

    private void HandleNoTarget(World world, Entity entity, ref Model model, ref Component component)
    {
        if (component.ElapsedTime < component.StartEnrage) { return; }
        if (component.ElapsedTime < component.Enrage)
        {
            if (component.StartEnrage <= -1) { return; }  // already started preparing enrage

            CastEnrage(entity, ref component);
        } else
        {
            if (component.Enrage <= -1)
            {  // already enraged
                Stand(ref model);
                if (component.ElapsedTime < 295) { component.ElapsedTime = 295; }
                return;
            }
            Enrage(world, entity, ref model, ref component);
        }
    }
}
