using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RaidsRewritten.Scripts.Attacks;

public class ADS(DalamudServices dalamud, CommonQueries commonQueries) : IAttack, ISystem
{
    public enum Phase
    {
        Omen,
        Animation,
        Snapshot,
        Vfx,
        Reset
    }

    public struct ADSEntity;
    public record struct Action(float ElapsedTime, Phase Phase = Phase.Omen);
    public record struct AnimationState(ushort Value, bool Interrupt = false);

    private const string ActionVfx = "vfx/monster/m0653/eff/m0653sp16_c0a1.avfx";
    private const string CastingVfx = "vfx/common/eff/mon_eisyo03t.avfx";
    private const ushort IdleAnimation = 34;
    private const ushort AttackAnimation = 2262;
    private const int ParalysisId = 0xBAD;
    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Omen, 0f },
        { Phase.Animation, 1.5f },
        { Phase.Snapshot, 2.2f },
        { Phase.Vfx, 2.2f },
        { Phase.Reset, 2.4f },
    };

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(316))
            .Set(new Position())
            .Set(new Rotation(0))
            .Set(new Scale())
            .Set(new UniformScale(0.5f))
            .Set(new AnimationState(IdleAnimation))
            .Add<ADSEntity>()
            .Add<Attack>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }

    public void Register(World world)
    {
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

        world.System<Action, Position, Rotation>().With<ADSEntity>().Each((Iter it, int i, ref Action component, ref Position position, ref Rotation rotation) =>
        {
            component.ElapsedTime += it.DeltaTime();

            var entity = it.Entity(i);

            switch (component.Phase)
            {
                case Phase.Omen:
                    if (ShouldReturn(component)) { return; }
                    AddActorVfx(entity, CastingVfx);
                    var omen = RectangleOmen.CreateEntity(world);
                    {
                        omen.Set(new Position(position.Value))
                            .Set(new Rotation(rotation.Value))
                            .Set(new Scale(new Vector3(0.75f, 1, 44)))
                            .ChildOf(entity);
                    }
                    component.Phase = Phase.Animation;
                    break;
                case Phase.Animation:
                    if (ShouldReturn(component)) { return; }
                    entity.Set(new AnimationState(AttackAnimation));
                    component.Phase = Phase.Snapshot;
                    break;
                case Phase.Snapshot:
                    if (ShouldReturn(component))
                    {
                        entity.Set(new AnimationState(IdleAnimation));
                        return;
                    }
                    entity.Children(child =>
                    {
                        if (!child.Has<Omen>()) { 
                            child.Destruct();
                            return;
                        }

                        var player = dalamud.ClientState.LocalPlayer;

                        if (player != null && !player.IsDead && RectangleOmen.IsInOmen(child, player.Position))
                        {
                            commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                            {
                                Paralysis.ApplyToPlayer(e, 30f, 3f, 1f, ParalysisId);
                            });
                        }
                        child.Destruct();
                    });
                    component.Phase = Phase.Vfx;
                    break;
                case Phase.Vfx:
                    if (ShouldReturn(component)) { return; }
                    AddActorVfx(entity, ActionVfx);
                    component.Phase = Phase.Reset;
                    break;
                case Phase.Reset:
                    if (ShouldReturn(component)) { return; }
                    it.Entity(i).Remove<Action>();
                    break;
            }
        });
    }

    public static bool CastLineAoe(Entity entity, float angle)
    {
        if (entity.Has<Action>()) { return false; }
        entity.Set(new Rotation(angle))
            .Set(new Action());
        return true;
    }

    public bool ShouldReturn(Action component)
    {
        if (phaseTimings.TryGetValue(component.Phase, out var phaseTiming))
        {
            if (component.ElapsedTime < phaseTiming) { return true; }
        }
        return false;
    }

    private static Entity AddActorVfx(Entity entity, string vfxPath)
    {
        return entity.CsWorld().Entity()
            .Set(new ActorVfx(vfxPath))
            .ChildOf(entity);
    }
}
