using System.Collections.Generic;
using System.Numerics;
using Flecs.NET.Core;
using RaidsRewritten.Extensions;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Components;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Spawn;

namespace RaidsRewritten.Scripts.Attacks;

public class ADS(DalamudServices dalamud, CommonQueries commonQueries, VfxSpawn vfxSpawn) : IAttack, ISystem
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
    public record struct LineAction(float Angle, float ElapsedTime = 0, Phase Phase = Phase.Omen);
    public record struct CircleAction(Vector3 TargetPosition1, Vector3? TargetPosition2, float ElapsedTime = 0, Phase Phase = Phase.Omen);

    private const float OmenDuration = 2.45f;
    private const string LineActionVfx = "vfx/monster/m0653/eff/m0653sp16_c0a1.avfx";
    private const string CastingVfx = "vfx/common/eff/mon_eisyo03t.avfx";
    private const string CircleActionVfx = "vfx/monster/gimmick2/eff/e3fa_b_g05c0j.avfx";
    private const ushort IdleAnimation = 34;
    private const ushort LineAttackAnimation = 2262;
    private const ushort CircleAttackAnimation = 2260;
    private const int ParalysisId = 0xBAD;
    private const float SnapshotEffectDelay = 0.25f;
    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Omen, 0f },
        { Phase.Animation, 1.85f },
        { Phase.Snapshot, 2.45f },
        { Phase.Vfx, 2.55f },
        { Phase.Reset, 2.55f },
    };

    public static Entity CreateEntity(World world)
    {
        return world.Entity()
            .Set(new Model(316))
            .Set(new AnimationState(1)) // ADS glow
            .Set(new Position())
            .Set(new Rotation(0))
            .Set(new Scale())
            .Set(new UniformScale(0.5f))
            .Set(new TimelineBase(IdleAnimation))
            .Add<ADSEntity>()
            .Add<Attack>();
    }

    public Entity Create(World world)
    {
        return CreateEntity(world);
    }

    public void Register(World world)
    {
        world.System<LineAction, Position, Rotation>().With<ADSEntity>().Each((Iter it, int i, ref LineAction component, ref Position position, ref Rotation rotation) =>
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
                            .Set(new Rotation(component.Angle))
                            .Set(new Scale(new Vector3(0.75f, 1, 44)))
                            .Set(new OmenDuration(OmenDuration, false))
                            .ChildOf(entity);
                    }
                    component.Phase = Phase.Animation;
                    break;
                case Phase.Animation:
                    if (ShouldReturn(component)) { return; }
                    entity.Set(new Rotation(component.Angle))
                        .Set(new TimelineBase(LineAttackAnimation));
                    component.Phase = Phase.Snapshot;
                    break;
                case Phase.Snapshot:
                    if (ShouldReturn(component))
                    {
                        entity.Set(new TimelineBase(IdleAnimation));
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
                            if (player.HasTranscendance())
                            {
                                DelayedAction.Create(world, () =>
                                {
                                    vfxSpawn.PlayInvulnerabilityEffect(player);
                                }, SnapshotEffectDelay);
                            }
                            else
                            {
                                commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                {
                                    DelayedAction.Create(e.CsWorld(), () =>
                                    {
                                        Paralysis.ApplyToTarget(e, 30f, 3f, 1f, ParalysisId);
                                    }, SnapshotEffectDelay);
                                });
                            }
                        }
                        child.Destruct();
                    });
                    component.Phase = Phase.Vfx;
                    break;
                case Phase.Vfx:
                    if (ShouldReturn(component)) { return; }
                    AddActorVfx(entity, LineActionVfx);
                    component.Phase = Phase.Reset;
                    break;
                case Phase.Reset:
                    if (ShouldReturn(component)) { return; }
                    it.Entity(i).Remove<LineAction>();
                    break;
            }
        });

        world.System<CircleAction, Position>().With<ADSEntity>()
            .Each((Iter it, int i, ref CircleAction component, ref Position _) =>
            {
                component.ElapsedTime += it.DeltaTime();

                var entity = it.Entity(i);

                switch (component.Phase)
                {
                    case Phase.Omen:
                        if (ShouldReturn(component)) { return; }
                        AddActorVfx(entity, CastingVfx);
                        CircleOmen.CreateEntity(world)
                            .Set(new Position(component.TargetPosition1))
                            .Set(new Scale(new Vector3(2f)))
                            .Set(new OmenDuration(OmenDuration, false))
                            .ChildOf(entity);

                        if (component.TargetPosition2.HasValue)
                        {
                            CircleOmen.CreateEntity(world)
                                .Set(new Position(component.TargetPosition2.Value))
                                .Set(new Scale(new Vector3(2f)))
                                .Set(new OmenDuration(OmenDuration, false))
                                .ChildOf(entity);
                        }

                        component.Phase = Phase.Animation;
                        break;
                    case Phase.Animation:
                        if (ShouldReturn(component)) { return; }
                        entity.Set(new TimelineBase(CircleAttackAnimation));
                        component.Phase = Phase.Snapshot;
                        break;
                    case Phase.Snapshot:
                        if (ShouldReturn(component))
                        {
                            entity.Set(new TimelineBase(IdleAnimation));
                            return;
                        }
                        entity.Children(child =>
                        {
                            if (!child.Has<Omen>())
                            {
                                child.Destruct();
                                return;
                            }

                            var player = dalamud.ClientState.LocalPlayer;

                            if (player != null && !player.IsDead && CircleOmen.IsInOmen(child, player.Position))
                            {
                                if (player.HasTranscendance())
                                {
                                    DelayedAction.Create(world, () =>
                                    {
                                        vfxSpawn.PlayInvulnerabilityEffect(player);
                                    }, SnapshotEffectDelay);
                                } else
                                {
                                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component _) =>
                                    {
                                        DelayedAction.Create(e.CsWorld(), () =>
                                        {
                                            Paralysis.ApplyToTarget(e, 30f, 3f, 1f, ParalysisId);
                                        }, SnapshotEffectDelay);
                                    });
                                }
                            }
                            child.Destruct();
                        });
                        component.Phase = Phase.Vfx;
                        break;
                    case Phase.Vfx:
                        if (ShouldReturn(component)) { return; }
                        FakeActor.Create(world)
                            .Set(new Position(component.TargetPosition1))
                            .Set(new ActorVfx(CircleActionVfx))
                            .ChildOf(entity);
                        if (component.TargetPosition2.HasValue)
                        {
                            FakeActor.Create(world)
                                .Set(new Position(component.TargetPosition2.Value))
                                .Set(new ActorVfx(CircleActionVfx))
                                .ChildOf(entity);
                        }
                        component.Phase = Phase.Reset;
                        break;
                    case Phase.Reset:
                        if (ShouldReturn(component)) { return; }
                        it.Entity(i).Remove<CircleAction>();
                        break;
                }
            });
    }

    public static bool CastLineAoe(Entity entity, float angle)
    {
        if (entity.Has<LineAction>() || entity.Has<CircleAction>()) { return false; }
        entity.Set(new LineAction(angle));
        return true;
    }

    public static bool CastSteppedLeader(Entity entity, Vector3 targetPosition1, Vector3? targetPosition2 = null)
    {
        if (entity.Has<LineAction>() || entity.Has<CircleAction>()) { return false; }
        entity.Set(new CircleAction(targetPosition1, targetPosition2));
        return true;
    }

    public bool ShouldReturn(LineAction component)
    {
        if (phaseTimings.TryGetValue(component.Phase, out var phaseTiming))
        {
            if (component.ElapsedTime < phaseTiming) { return true; }
        }
        return false;
    } 
    public bool ShouldReturn(CircleAction component)
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
