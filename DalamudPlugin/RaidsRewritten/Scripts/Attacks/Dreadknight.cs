using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using Flecs.NET.Bindings;
using Flecs.NET.Core;
using NLog;
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
using static Lumina.Data.Files.ScdFile;
using Player = RaidsRewritten.Game.Player;

namespace RaidsRewritten.Scripts.Attacks;

public class Dreadknight(DalamudServices dalamud) : IAttack, IDisposable, ISystem
{
    public record struct Component(float ElapsedTime, float NextRefresh, float StartEnrage = 7f, float Enrage = 12f);

    private const ushort WalkingAnimation = 41;
    private const ushort AttackAnimation = 1515;
    private const ushort EnrageAnimation = 1516;
    private const float HitboxRadius = 2.5f;

    private const float MovementSpeed = 2.5f;
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
        return world.Entity()
                .Set(new Model(379))
                .Set(new Rotation(0))
                .Set(new Scale())
                .Set(new UniformScale(1f))
                .Set(new Component(0, 0))
                .Set(new Position())
                .Add<Attack>();
    }

    public void Dispose()
    {
        this.playerQuery.Dispose();
    }

    public void Register(World world)
    {
        this.playerQuery = Player.Query(world);

        world.System<Model, Component, Position, Rotation>()
            .Each((Iter it, int i, ref Model model, ref Component component, ref Position position, ref Rotation rotation) =>
            {
                component.ElapsedTime += it.DeltaTime();
                var entity = it.Entity(i);

                if (component.ElapsedTime < InitialDelay) { return; }  // only want to start looking at player and chasing when ready 

                if (entity.TryGet<ActorVfxTarget>(out var target))
                {
                    if (target.Target != null && (target.Target.IsValid() || !target.Target.IsDead))
                    {
                        // face player
                        var sourcePosV2 = new Vector2(position.Value.X, position.Value.Z);
                        var targetPosV2 = new Vector2(target.Target.Position.X, target.Target.Position.Z);
                        var angle = MathUtilities.GetAbsoluteAngleFromSourceToTarget(sourcePosV2, targetPosV2);
                        rotation.Value = angle;

                        if (component.ElapsedTime < component.NextRefresh) {
                            Stand(ref model);
                            return;
                        }

                        if (Vector2.DistanceSquared(sourcePosV2, targetPosV2) < HitboxRadius)
                        {
                            Hit(world, ref model, ref component);
                        } else
                        {
                            Follow(it, ref model, ref component, ref position, angle);
                        }
                    } else
                    {
                        // target just died
                        entity.Remove<ActorVfx>();
                        entity.Remove<ActorVfxTarget>();
                        component.StartEnrage = component.ElapsedTime + 10;
                    }
                } else
                {
                    HandleNoTarget(world, entity, ref model, ref component);
                }

                if (component.ElapsedTime > 300)
                {
                    entity.Destruct();
                }
            });
    }

    public static void ApplyTether(Entity entity, IGameObject target)
    {
        entity.Remove<ActorVfx>();

        // need to wait a bit for any existing vfx to be removed
        DelayedAction.Create(entity.CsWorld(), () =>
        {
            entity.Set(new ActorVfx("vfx/channeling/eff/chn_tergetfix1f.avfx"))
                .Set(new ActorVfxTarget(target));
        }, 0.5f);
            
    }

    private void SetTimeline(Model model, ushort animationId)
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

    // TODO: implement this
    private void ShowToast(string text) { }

    private void StunPlayer(World world, float duration, float delay = StunDelay)
    {
        this.playerQuery.Each((Entity e, ref Player.Component _) =>
        {
            DelayedAction.Create(world, () => {
                Stun.ApplyToPlayer(e, duration, StunId);
            }, delay);
        });
    }

    private void Stand(ref Model model)
    {
        SetTimeline(model, 0);
    }

    private void Follow(Iter it, ref Model model, ref Component component, ref Position position, float angle)
    {
        SetTimeline(model, WalkingAnimation);
        var newPosition = position.Value;
        newPosition.Z += MovementSpeed * it.DeltaTime() * MathF.Cos(angle);
        newPosition.X += MovementSpeed * it.DeltaTime() * MathF.Sin(angle);
        position.Value = newPosition;
    }

    private void Hit(World world, ref Model model, ref Component component)
    {
        SetTimeline(model, AttackAnimation);
        StunPlayer(world, StunDuration);
        component.NextRefresh = component.ElapsedTime + 3f;
    }

    private void CastEnrage(Entity entity, ref Component component)
    {
        DelayedAction.Create(entity.CsWorld(), () =>
        {
            entity.Set(new ActorVfx("vfx/common/eff/mon_eisyo03t.avfx"));
        }, 0.5f);
        component.StartEnrage = -1;
        component.Enrage = component.ElapsedTime + 5;
        
    }

    private void Enrage(World world, Entity entity, ref Model model, ref Component component)
    {
        entity.Remove<ActorVfx>();
        ShowTextGimmick("Enraged without the sight of resistance, the Dreadknight lets out a deafening shrill!", EnrageNotificationDuration);
        SetTimeline(model, EnrageAnimation);
        DelayedAction.Create(world, () => entity.Set(new ActorVfx("vfx/monster/m0150/eff/m150sp003c1m.avfx")), EnrageVfxDelay);
        //entity.Set(new ActorVfx("vfx/monster/m0150/eff/m150sp003c0m.avfx"));
        StunPlayer(world, EnrageStunDuration, EnrageStunDelay);
        component.Enrage = -1;
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
