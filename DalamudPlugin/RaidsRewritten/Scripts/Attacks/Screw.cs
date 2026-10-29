using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Attacks;

public class Screw(DalamudServices dalamud, Lazy<EntityManager> entityManager, Random random, ILogger logger) : IEntity, ISystem
{
    public const float LinkRadius = 1.9f;

    public enum Direction : int
    {
        Clockwise = 0,
        Counterclockwise = 1,
    }
    public record struct Component(bool ShowOmen, Direction Direction, ICharacter Target,
        Action<Component, ulong>? AttemptLink = null, Action<Component>? RemoveLink = null,
        Action<Component, int>? UpdateRotationCount = null, Action<Component, bool>? Resolve = null);
    public record struct LinkedTo(ICharacter Target);
    public record struct ServerState(int RotationCount = 0, bool ResolvedSuccess = false, bool ResolvedFail = false, ulong LinkedTo = 0);
    // For debug purposes
    public struct ClientControlled;

    private enum Phase
    {
        Omen,
        Active,
        ResolvedSuccess,
        ResolvedFail,
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
        float LinkRotation = default,
        float AttemptLinkActionCooldown = default,
        float RemoveLinkActionCooldown = default);
    private record struct BoltRuntime(
        float AnimationTime = 0,
        Entity Model = default,
        Vector3 StartPosition = default,
        Quaternion StartRotation = default,
        Vector3 FallDirection = default);

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

                // There's a completely inexplicable bug here:
                // If runtime.GroundIndicator is assigned an entity with at least 2 components/tags,
                // Then runtime.RotationIndicator is assigned an entity with at least 1 component,
                // THEN an entity with at least 1 component is assigned as a child of runtime.RotationIndicator,
                // AND neither runtime.GroundIndicator nor runtime.RotationIndicator are assigned as children of entity,
                // when the Runtime OnRemove observer fires, runtime.RotationIndicator.SafeDestruct() will CRASH.

                runtime.ElapsedTime += it.DeltaTime();
                runtime.AttemptLinkActionCooldown = MathF.Max(runtime.AttemptLinkActionCooldown - it.DeltaTime(), 0);
                runtime.RemoveLinkActionCooldown = MathF.Max(runtime.RemoveLinkActionCooldown - it.DeltaTime(), 0);

                if (component.Target == null || !component.Target.IsValid() || component.Target.IsDead)
                {
                    entity.Destruct();
                    return;
                }

                // LinkedTo
                if (!entity.Has<LinkedTo>())
                {
                    runtime.LinkVfx.SafeDestruct();
                    runtime.RemoveLinkActionCooldown = 0;
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
                            omen.ChildOf(entity);
                            runtime.Omen = omen;
                        }
                        return;
                    }
                }

                // Active
                if (runtime.Phase == Phase.Active)
                {
                    if (!entity.Has<BoltRuntime>()) { entity.Set(new BoltRuntime()); }

                    if (!runtime.GroundIndicator.IsValid())
                    {
                        var vfxPath = "vfx/common/eff/ev_lockon_01x.avfx";
                        runtime.GroundIndicator = FakeActor.CreateZeroAlphaModel(it.World())
                            .Set(new ActorVfx(vfxPath))
                            .Set(new FollowPosition(component.Target))
                            .ChildOf(entity);

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

                    var rotationNumber = 3;
                    var resolvedSuccess = false;
                    var resolvedFail = false;
                    ulong linkedToId = 0;

                    var linkIsSelf = false;
                    if (entity.TryGet(out ServerState serverState))
                    {
                        linkedToId = serverState.LinkedTo;
                        var player = dalamud.ObjectTable.LocalPlayer;
                        linkIsSelf = player?.GetContentId() == serverState.LinkedTo;
                    }
                    var clientAuthoritative = linkIsSelf || entity.Has<ClientControlled>();

                    if (clientAuthoritative)
                    {
                        rotationNumber = (int)(runtime.Rotation / (2 * MathF.PI));
                        rotationNumber = Math.Max(3 - Math.Abs(rotationNumber), 0);
                    }
                    else if (entity.TryGet(out serverState))
                    {
                        rotationNumber = serverState.RotationCount;
                        resolvedSuccess = serverState.ResolvedSuccess;
                        resolvedFail = serverState.ResolvedFail;
                    }

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
                                .Set(new ExpectFileReplacement())
                                .ChildOf(entity);

                            var replacementPath = dalamud.PluginInterface.GetResourcePath(vfxPathReplacement);
                            it.World().Entity()
                                .Set(new FileReplacement(vfxPath, replacementPath))
                                .ChildOf(runtime.RotationIndicator);

                            if (clientAuthoritative)
                            {
                                component.UpdateRotationCount?.Invoke(component, rotationNumber);
                            }
                        }
                        else if (clientAuthoritative)
                        {
                            // Resolve
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
                                resolvedSuccess = true;
                            }
                            else
                            {
                                resolvedFail = true;
                            }
                            component.Resolve?.Invoke(component, success);
                        }
                    }

                    if (resolvedSuccess || resolvedFail)
                    {
                        runtime.RotationIndicator.SafeDestruct();
                        runtime.GroundIndicator.SafeDestruct();
                        ref var boltRuntime = ref entity.GetMut<BoltRuntime>();
                        boltRuntime.AnimationTime = 0;

                        if (resolvedSuccess)
                        {
                            it.World().Entity()
                                .Set(new ActorVfx("vfx/lockon/eff/m0489trg_a0c.avfx"))
                                .Set(new ActorVfxSource(component.Target));
                            runtime.Phase = Phase.ResolvedSuccess;
                        }
                        else if (resolvedFail)
                        {
                            it.World().Entity()
                                .Set(new ActorVfx("vfx/lockon/eff/m0489trg_b0c.avfx"))
                                .Set(new ActorVfxSource(component.Target));
                            it.World().Entity()
                                .Set(new ActorVfx("vfx/monster/m0005/eff/m0005sp_15t0t.avfx"))
                                .Set(new ActorVfxSource(component.Target));
                            runtime.Phase = Phase.ResolvedFail;
                        }
                        return;
                    }

                    if (!entity.Has<LinkedTo>())
                    {
                        if (linkedToId != 0)
                        {
                            using var q = it.World().Query<Player.Component, Player.ContentId>();
                            var player = q.Find((ref pc, ref contentId) => pc.PlayerCharacter != null && contentId.Value == linkedToId);
                            if (player.IsValid() && player.TryGet(out Player.Component pc))
                            {
                                entity.Set(new LinkedTo(pc.PlayerCharacter!));
                            }
                        }
                        else
                        {
                            var player = dalamud.ObjectTable.LocalPlayer;
                            if (player != null && !player.IsDead &&
                                component.Target.GameObjectId != player.GameObjectId)
                            {
                                if (Vector2.Distance(component.Target.Position2, player.Position2) <= LinkRadius)
                                {
                                    if (!entity.Has<ClientControlled>())
                                    {
                                        if (runtime.AttemptLinkActionCooldown == 0)
                                        {
                                            component.AttemptLink?.Invoke(component, player.GetContentId());
                                            runtime.AttemptLinkActionCooldown = 3.0f;
                                        }
                                    }
                                    else
                                    {
                                        entity.Set(new LinkedTo(player));
                                    }
                                }
                            }
                        }
                    }
                }

                // Resolve
                if (runtime.Phase == Phase.ResolvedSuccess ||
                    runtime.Phase == Phase.ResolvedFail)
                {
                    if (!entity.Has<BoltRuntime>())
                    {
                        entity.Destruct();
                    }
                }

            });

        world.System<Component, LinkedTo, Runtime>()
            .Each((Iter it, int i, ref Component component, ref LinkedTo linkedTo, ref Runtime runtime) =>
            {
                var entity = it.Entity(i);

                if (runtime.Phase != Phase.Active || linkedTo.Target == null || !linkedTo.Target.IsValid())
                {
                    entity.Remove<LinkedTo>();
                    return;
                }

                if (entity.TryGet(out ServerState serverState))
                {
                    if (serverState.LinkedTo == 0 ||
                        linkedTo.Target.GetContentId() != serverState.LinkedTo)
                    {
                        entity.Remove<LinkedTo>();
                        return;
                    }
                }

                if (linkedTo.Target.IsDead ||
                    Vector2.Distance(component.Target.Position2, linkedTo.Target.Position2) > LinkRadius)
                {
                    var player = dalamud.ObjectTable.LocalPlayer;
                    if (!entity.Has<ClientControlled>())
                    {
                        if (linkedTo.Target.GameObjectId == player?.GameObjectId &&
                            runtime.RemoveLinkActionCooldown == 0)
                        {
                            component.RemoveLink?.Invoke(component);
                            runtime.RemoveLinkActionCooldown = 3.0f;
                        }
                    }
                    else
                    {
                        entity.Remove<LinkedTo>();
                    }
                    return;
                }

                var linkRotation = MathUtilities.VectorToRotation(linkedTo.Target.Position2 - component.Target.Position2);

                if (!runtime.LinkVfx.IsValid())
                {
                    runtime.LinkVfx = it.World().Entity()
                        .Set(new ActorVfx("vfx/channeling/eff/chn_elem0f.avfx"))
                        .Set(new ActorVfxSource(component.Target))
                        .Set(new ActorVfxTarget(linkedTo.Target))
                        .ChildOf(entity);
                }
                else
                {
                    var rotationDelta = MathUtilities.GetShortestRotationDirection(linkRotation, runtime.LinkRotation);
                    runtime.Rotation -= rotationDelta;
                }
                runtime.LinkRotation = linkRotation;
            });

        world.System<Component, Runtime, BoltRuntime>()
            .Each((Iter it, int i, ref Component component, ref Runtime runtime, ref BoltRuntime boltRuntime) =>
            {
                var entity = it.Entity(i);

                if (!boltRuntime.Model.IsValid() && entityManager.Value.TryCreateEntity<Bolt>(out Entity bolt))
                {
                    bolt.Set(new UniformScale(1.8f)).ChildOf(entity);
                    boltRuntime.Model = bolt;
                }

                var activeBoltY = -0.8f;

                float CalcRotationOffset(float y)
                {
                    return (y - activeBoltY) * 4f * MathF.PI;
                }

                if (runtime.Phase == Phase.Active)
                {
                    var startY = 3.0f;
                    var duration = 1.25f;
                    var t = Math.Clamp(boltRuntime.AnimationTime / duration, 0, 1);
                    var y = MathUtilities.Tween(startY, activeBoltY, t, MathUtilities.Ease.EaseOutCirc);
                    var position = component.Target.Position + y * Vector3.UnitY;
                    boltRuntime.Model.Set(new Position(position));

                    var r = CalcRotationOffset(y);
                    if (component.Direction == Direction.Counterclockwise)
                    {
                        r *= -1;
                    }
                    var rotation = runtime.Rotation + r;
                    boltRuntime.Model.Set(new Rotation(rotation));
                }
                else if (runtime.Phase == Phase.ResolvedFail)
                {
                    var endY = -1.2f;
                    var duration = 1.2f;
                    var t = Math.Clamp(boltRuntime.AnimationTime / duration, 0, 1);
                    var y = MathUtilities.Tween(activeBoltY, endY, t, MathUtilities.Ease.Linear);
                    var position = component.Target.Position + y * Vector3.UnitY;
                    boltRuntime.Model.Set(new Position(position));

                    var r = CalcRotationOffset(y);
                    if (component.Direction == Direction.Counterclockwise)
                    {
                        r *= -1;
                    }
                    var rotation = runtime.Rotation + r;
                    boltRuntime.Model.Set(new Rotation(rotation));

                    if (boltRuntime.AnimationTime > 3.0f)
                    {
                        entity.Destruct();
                    }
                }
                else if (runtime.Phase == Phase.ResolvedSuccess)
                {
                    if (boltRuntime.AnimationTime == 0)
                    {
                        boltRuntime.StartPosition = boltRuntime.Model.Get<Position>().Value;
                        boltRuntime.StartRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, runtime.Rotation);
                        boltRuntime.FallDirection = MathUtilities.RotationToUnitVector(random.NextSingle() * 2 * MathF.PI).ToVector3(0);
                    }

                    var position = boltRuntime.StartPosition;
                    var a = -35.0f;
                    var v_i = 8.5f;
                    var y_i = position.Y;
                    var y_ground = position.Y - activeBoltY;
                    var t_end = (-v_i - MathF.Sqrt((v_i * v_i) - (2 * a * activeBoltY))) / a;

                    var v_x = 2.5f;
                    var rotationAxis = Vector3.Transform(boltRuntime.FallDirection, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f * MathF.PI));
                    var targetRotation = 0.4f * MathF.PI;
                    var rotationSpeed = targetRotation / t_end;
                    //logger.Info("t_end1 {0}, t_end2 {1}", t_end, t_end2);

                    float x;
                    float y;
                    Quaternion r_d;

                    if (boltRuntime.AnimationTime < t_end)
                    {
                        x = v_x * boltRuntime.AnimationTime;
                        float CalcY(float t)
                        {
                            return 0.5f * a * t * t + v_i * t + y_i;
                        }
                        y = CalcY(boltRuntime.AnimationTime);
                        var t = MathUtilities.Tween(0, t_end, boltRuntime.AnimationTime / t_end, MathUtilities.Ease.EaseInQuad);
                        r_d = Quaternion.CreateFromAxisAngle(rotationAxis, rotationSpeed * t);
                    }
                    else
                    {
                        x = v_x * t_end;
                        y = y_ground;
                        r_d = Quaternion.CreateFromAxisAngle(rotationAxis, rotationSpeed * t_end);
                    }

                    position += x * boltRuntime.FallDirection;
                    position.Y = y;
                    boltRuntime.Model.Set(new Position(position));

                    var rotation = r_d * boltRuntime.StartRotation;
                    boltRuntime.Model.Set(new FullRotation(rotation));

                    if (boltRuntime.AnimationTime > 3.0f)
                    {
                        entity.Destruct();
                    }
                }

                boltRuntime.AnimationTime += it.DeltaTime();
            });
    }
}
