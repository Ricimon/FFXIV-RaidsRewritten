using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Attacks;

public class ArticulatedBit(CommonQueries commonQueries) : IEntity, ISystem
{
    public enum ModelType
    {
        LeftHand,
        RightHand,
    }

    public enum Phase
    {
        Start,
        Omen,
        Snapshot,
        ProjectileShoot,
        ProjectileHit,
    }

    public record struct Component(
        ModelType ModelType = ModelType.LeftHand,
        List<IGameObject>? Targets = null,
        float DistanceThreshold = 10.0f);

    private class TargetData
    {
        public IGameObject? Target;
        public Entity CloseTether;
        public Entity FarTether;
        public bool ApplyPunishment;
    }
    private record struct Runtime(
        float ElapsedTime = 0f,
        Phase Phase = Phase.Start,
        Entity Model = default,
        List<TargetData>? TargetData = null);

    private readonly Dictionary<Phase, float> phaseTimings = new()
    {
        { Phase.Start, 0.5f },
        { Phase.Omen, 2.0f },
        { Phase.Snapshot, 0.3f },
        { Phase.ProjectileShoot, 0.35f },
        { Phase.ProjectileHit, 0.8f },
    };

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Component())
            .Set(new Runtime())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Runtime, Position, Rotation>()
            .Each((Iter it, int i, ref Component component, ref Runtime runtime, ref Position position, ref Rotation rotation) =>
            {
                var entity = it.Entity(i);

                runtime.ElapsedTime += it.DeltaTime();

                var totalPhaseDuration = GetTotalPhaseDuration(runtime.Phase);
                switch (runtime.Phase)
                {
                    case Phase.Start:
                        if (!runtime.Model.IsValid())
                        {
                            runtime.Model = it.World().Entity()
                                .Set(new Model(component.ModelType == ModelType.LeftHand ? 3257 : 3256))
                                .Set(new Position(position.Value))
                                .Set(new Rotation(rotation.Value))
                                .Set(new UniformScale(1.0f))
                                .Set(new TimelineBlend(1, 4561))
                                .ChildOf(entity);

                            it.World().Entity()
                                .Set(new ActorVfx("vfx/monster/m0729/eff/m729show_sp01c0t1.avfx"))
                                .ChildOf(runtime.Model);
                        }

                        if (component.Targets != null)
                        {
                            runtime.TargetData ??= component.Targets.AsValueEnumerable()
                                .Select(t => new TargetData { Target = t }).ToList();
                        }

                        if (runtime.ElapsedTime >= totalPhaseDuration)
                        {
                            runtime.Phase = Phase.Omen;
                        }
                        break;
                    case Phase.Omen:
                        if (runtime.Model.IsValid() && runtime.TargetData != null)
                        {
                            IGameObject? rotationTarget = null;
                            foreach (var targetData in runtime.TargetData)
                            {
                                var target = targetData.Target;
                                if (target == null) { continue; }

                                rotationTarget ??= target;

                                var distanceToTarget = MathUtilities.Vector2Distance(position.Value, target.Position);
                                if (distanceToTarget < component.DistanceThreshold)
                                {
                                    targetData.FarTether.SafeDestruct();
                                    if (!targetData.CloseTether.IsValid())
                                    {
                                        targetData.CloseTether = it.World().Entity()
                                            .Set(new ActorVfx("vfx/channeling/eff/chn_arrow01f.avfx"))
                                            .Set(new ActorVfxTarget(target))
                                            .ChildOf(runtime.Model);
                                        targetData.ApplyPunishment = true;
                                    }
                                }
                                else
                                {
                                    targetData.CloseTether.SafeDestruct();
                                    if (!targetData.FarTether.IsValid())
                                    {
                                        targetData.FarTether = it.World().Entity()
                                            .Set(new ActorVfx("vfx/channeling/eff/chn_dark001f.avfx"))
                                            .Set(new ActorVfxTarget(target))
                                            .ChildOf(runtime.Model);
                                        targetData.ApplyPunishment = false;
                                    }
                                }
                            }

                            if (rotationTarget != null)
                            {
                                var v = rotationTarget.Position2 - position.Value.ToVector2();
                                var r = MathUtilities.VectorToRotation(v);
                                runtime.Model.Set(new Rotation(r));
                            }
                        }

                        if (runtime.ElapsedTime >= totalPhaseDuration)
                        {
                            runtime.Phase = Phase.Snapshot;

                            if (runtime.TargetData != null)
                            {
                                foreach (var targetData in runtime.TargetData)
                                {
                                    targetData.CloseTether.SafeDestruct();
                                    targetData.FarTether.SafeDestruct();
                                }
                            }

                            if (runtime.Model.IsValid())
                            {
                                runtime.Model.Set(new OneTimeModelTimeline(3202));
                            }
                        }
                        break;

                    case Phase.Snapshot:
                        if (runtime.ElapsedTime < totalPhaseDuration) { break; }
                        runtime.Phase = Phase.ProjectileShoot;

                        if (runtime.Model.IsValid() && runtime.TargetData != null)
                        {
                            foreach (var targetData in runtime.TargetData)
                            {
                                it.World().Entity()
                                    .Set(new ActorVfx("vfx/monster/m0729/eff/m0729_sp01c0t2.avfx"))
                                    .Set(new ActorVfxTarget(targetData.Target))
                                    .ChildOf(runtime.Model);
                            }
                        }
                        break;

                    case Phase.ProjectileShoot:
                        if (runtime.ElapsedTime < totalPhaseDuration) { break; }
                        runtime.Phase = Phase.ProjectileHit;

                        if (runtime.Model.IsValid() && runtime.TargetData != null)
                        {
                            foreach (var targetData in runtime.TargetData)
                            {
                                var target = targetData.Target;
                                if (target == null) { continue; }

                                it.World().Entity()
                                    .Set(new ActorVfx("vfx/monster/m0729/eff/m0729_sp01t0t2.avfx"))
                                    .Set(new ActorVfxSource(target))
                                    .ChildOf(entity);

                                if (targetData.ApplyPunishment)
                                {
                                    commonQueries.LocalPlayerQuery.Each((Entity e, ref Player.Component player) =>
                                    {
                                        if (player.PlayerCharacter?.GameObjectId == target.GameObjectId)
                                        {
                                            Stun.ApplyToTarget(e, 10.0f);
                                        }
                                    });
                                }
                            }
                        }
                        break;

                    case Phase.ProjectileHit:
                        if (runtime.ElapsedTime < totalPhaseDuration) { break; }
                        entity.Destruct();
                        return;
                }
            });
    }

    private float GetTotalPhaseDuration(Phase phase)
    {
        float duration = 0;
        while (phase >= 0)
        {
            if (phaseTimings.TryGetValue(phase, out var d))
            {
                duration += d;
            }
            phase -= 1;
        }
        return duration;
    }
}
