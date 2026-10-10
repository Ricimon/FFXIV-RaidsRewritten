using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class Screw(DalamudServices dalamud, Lazy<EntityManager> entityManager) : IEntity, ISystem
{
    public const float LinkRadius = 1.9f;

    public enum Direction
    {
        Clockwise,
        Counterclockwise,
    }
    public record struct Component(bool ShowOmen, Direction Direction, IGameObject Target);
    public record struct LinkedTo(IGameObject Target);

    private enum Phase
    {
        Omen,
        Active,
        Resolved,
    }
    private record struct Runtime(
        float ElapsedTime = 0,
        Phase Phase = Phase.Omen,
        float Rotation = 0,
        Entity Omen = default,
        Entity RotationIndicator = default,
        int RotationIndicatorNumber = default,
        Entity GroundIndicator = default,
        Entity LinkVfx = default,
        float LinkRotation = default);

    public Entity Create(World world)
    {
        return world.Entity()
            .Set(new Component())
            .Set(new Runtime())
            .Add<Attack>();
    }

    public void Register(World world)
    {
        world.System<Component, Runtime>()
            .Each((Iter it, int i, ref Component component, ref Runtime runtime) =>
            {
                var entity = it.Entity(i);

                runtime.ElapsedTime += it.DeltaTime();

                if (component.Target == null || !component.Target.IsValid() || component.Target.IsDead)
                {
                    entity.Destruct();
                    return;
                }

                var timeThreshold = 0.0f;

                // Omen
                if (component.ShowOmen)
                {
                    timeThreshold += RotatingLockOnOmen.OmenDuration;

                    if (runtime.Phase == Phase.Omen)
                    {
                        if (runtime.ElapsedTime >= timeThreshold)
                        {
                            runtime.Phase = Phase.Active;
                            return;
                        }

                        if (!runtime.Omen.IsValid() && entityManager.Value.TryCreateEntity<RotatingLockOnOmen>(out Entity omen))
                        {
                            switch (component.Direction)
                            {
                                case Direction.Clockwise:
                                    omen.Set(new RotatingLockOnOmen.Component(RotatingLockOnOmen.Direction.Clockwise,
                                        component.Target));
                                    break;
                                case Direction.Counterclockwise:
                                    omen.Set(new RotatingLockOnOmen.Component(RotatingLockOnOmen.Direction.Counterclockwise,
                                        component.Target));
                                    break;
                            }
                            runtime.Omen = omen;
                        }
                        return;
                    }
                }

                // Active
                if (runtime.Phase == Phase.Active)
                {
                    if (!runtime.GroundIndicator.IsValid())
                    {
                        var vfxPath = "vfx/common/eff/ev_lockon_01x.avfx";
                        runtime.GroundIndicator = FakeActor.CreateZeroAlphaModel(it.World())
                            .Set(new ActorVfx(vfxPath))
                            .Set(new FollowPosition(component.Target));

                        var replacementPath = dalamud.PluginInterface.GetResourcePath("vfx/ground_lockon.avfx");
                        it.World().Entity()
                            .Set(new FileReplacement(vfxPath, replacementPath))
                            .ChildOf(runtime.GroundIndicator);

                        //if (entityManager.Value.TryCreateEntity<CircleOmen>(out Entity circle))
                        //{
                        //    circle.Set(new Position(component.Target.Position));
                        //    circle.Set(new Scale(1.9f * Vector3.One));
                        //}
                    }

                    runtime.GroundIndicator.Set(new Rotation(runtime.Rotation));

                    var rotationNumber = (int)(runtime.Rotation / (2 * MathF.PI));
                    rotationNumber = Math.Max(3 - Math.Abs(rotationNumber), 0);
                    if (rotationNumber != runtime.RotationIndicatorNumber)
                    {
                        runtime.RotationIndicator.SafeDestruct();
                        runtime.RotationIndicatorNumber = rotationNumber;
                        if (rotationNumber > 0)
                        {
                            var vfxPath = rotationNumber switch
                            {
                                1 => "vfx/lockon/eff/com_s8count03x.avfx",
                                2 => "vfx/lockon/eff/m0146_count3_t0j2.avfx",
                                _ => "vfx/lockon/eff/m0296_com_s5count3g2.avfx",
                            };
                            var vfxPathReplacement = rotationNumber switch
                            {
                                1 => "vfx/count_1_of_3.avfx",
                                2 => "vfx/count_2_of_3.avfx",
                                _ => "vfx/count_3_of_3.avfx",
                            };
                            runtime.RotationIndicator = it.World().Entity()
                                .Set(new ActorVfx(vfxPath))
                                .Set(new ActorVfxSource(component.Target))
                                .Set(new ExpectFileReplacement());

                            var replacementPath = dalamud.PluginInterface.GetResourcePath(vfxPathReplacement);
                            it.World().Entity()
                                .Set(new FileReplacement(vfxPath, replacementPath))
                                .ChildOf(runtime.RotationIndicator);
                        }
                        else
                        {
                            // Resolve
                            runtime.Phase = Phase.Resolved;
                            runtime.RotationIndicator.SafeDestruct();
                            runtime.GroundIndicator.SafeDestruct();

                            bool success = false;
                            switch (component.Direction)
                            {
                                case Direction.Clockwise:
                                    success = runtime.Rotation > 0; break;
                                case Direction.Counterclockwise:
                                    success = runtime.Rotation < 0; break;
                            }

                            if (success)
                            {
                                it.World().Entity()
                                    .Set(new ActorVfx("vfx/lockon/eff/m0489trg_a0c.avfx"))
                                    .Set(new ActorVfxSource(component.Target));
                            }
                            else
                            {
                                it.World().Entity()
                                    .Set(new ActorVfx("vfx/lockon/eff/m0489trg_b0c.avfx"))
                                    .Set(new ActorVfxSource(component.Target));
                                it.World().Entity()
                                    .Set(new ActorVfx("vfx/monster/m0005/eff/m0005sp_15t0t.avfx"))
                                    .Set(new ActorVfxSource(component.Target));
                            }
                            return;
                        }
                    }

                    if (!entity.Has<LinkedTo>())
                    {
                        var player = dalamud.ObjectTable.LocalPlayer;
                        if (player != null && !player.IsDead)
                        {
                            if (Vector2.Distance(component.Target.Position2, player.Position2) <= LinkRadius)
                            {
                                entity.Set(new LinkedTo(player));
                            }
                        }
                    }
                }

                // Resolve
                if (runtime.Phase == Phase.Resolved)
                {
                    entity.Destruct();
                }
            });

        world.System<Component, LinkedTo, Runtime>()
            .Each((Iter it, int i, ref Component component, ref LinkedTo linkedTo, ref Runtime runtime) =>
            {
                var entity = it.Entity(i);

                if (runtime.Phase != Phase.Active ||
                    linkedTo.Target == null || !linkedTo.Target.IsValid() || linkedTo.Target.IsDead ||
                    Vector2.Distance(component.Target.Position2, linkedTo.Target.Position2) > LinkRadius)
                {
                    runtime.LinkVfx.SafeDestruct();
                    entity.Remove<LinkedTo>();
                    return;
                }

                var linkRotation = MathUtilities.VectorToRotation(linkedTo.Target.Position2 - component.Target.Position2);

                if (!runtime.LinkVfx.IsValid())
                {
                    runtime.LinkVfx = it.World().Entity()
                        .Set(new ActorVfx("vfx/channeling/eff/chn_elem0f.avfx"))
                        .Set(new ActorVfxSource(component.Target))
                        .Set(new ActorVfxTarget(linkedTo.Target));
                }
                else
                {
                    var rotationDelta = MathUtilities.GetShortestRotationDirection(linkRotation, runtime.LinkRotation);
                    runtime.Rotation -= rotationDelta;
                }
                runtime.LinkRotation = linkRotation;
            });

        world.Observer<Runtime>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Runtime _) =>
            {
                var runtime = e.Get<Runtime>();
                runtime.Omen.SafeDestruct();
                runtime.RotationIndicator.SafeDestruct();
                runtime.GroundIndicator.SafeDestruct();
                runtime.LinkVfx.SafeDestruct();
            });
    }
}
