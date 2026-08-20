using System.Collections.Generic;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class ShanoaParkTest : Mechanic
{
    private const uint EdensGravityActionId = 15728;
    private const uint ViceAndVirtueActionId = 17647;
    private const uint AetherCompassActionId = 26988;

    private const string MarkerAttackVfxPath = "vfx/monster/d1024/eff/arthur_thunderstorm_t0s.avfx";
    private const string AetherCompassLocationVfxPath = "bg/ex2/05_zon_z3/common/vfx/eff/b1526bari1_u.avfx";
    private const string AetherCompassLocationArrowsVfxPath = "bgcommon/world/common/vfx_for_bg/eff/b1490tagt1_o.avfx";

    private const float GuidanceMarkerRadius = 1.4f;

    private readonly List<Entity> attacks = [];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);
    private readonly List<Entity> guidanceEntities = [];
    private readonly HashSet<int> availableGuidanceMarkers = [];

    private Entity shanoa;
    private Entity fireTornado;
    private bool edensGravityCasted = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
        ClearGuidanceEntities();
        edensGravityCasted = false;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatStart()
    {
        var fireTornadoPosition = new Vector3(87.0f, 0.0f, 113.0f);
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                requestId = NetworkMechanic.TeaSpawnShanoa.ToString(),
                mechanicId = (uint)NetworkMechanic.TeaSpawnShanoa,
                worldPositionX = fireTornadoPosition.X,
                worldPositionY = fireTornadoPosition.Y,
                worldPositionZ = fireTornadoPosition.Z,
                rotation = default(float),
            }
        }).SafeFireAndForget();

        if (EntityManager.TryCreateEntity<FireTornadoEntity>(out var tornado))
        {
            tornado.Set(new Position(fireTornadoPosition));
            attacks.Add(tornado);
            fireTornado = tornado;
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnStartingCast(Action action, IBattleChara source)
    {
        if (!edensGravityCasted && action.RowId == EdensGravityActionId)
        {
            edensGravityCasted = true;

            if (fireTornado.IsValid())
            {
                FireTornadoEntity.NetworkedAttack1(fireTornado, typeof(FireTornadoEntity.NetworkedAttack1Trigger).FullName! + "_1");
            }

            //if (!shanoa.IsValid()) { return; }
            //var fireTornado = World.Query<FireTornadoEntity.Component>().First();
            //if (!fireTornado.IsValid()) { return; }
            //if (shanoa.TryGet(out Model shanoaModel) && fireTornado.TryGet(out Model fireTornadoModel))
            //{
            //    var tether = World.Entity().Set(new TetherOmen.ProximityTether(
            //        DistanceThreshold: 10.0f,
            //        Source: fireTornadoModel.GameObject, Target: shanoaModel.GameObject))
            //        .ChildOf(fireTornado);
            //    attacks.Add(tether);
            //}

            AttackMarkers();

            using var q = World.Query<FireTornadoEntity.Component, Position>();
            var ranOnce = false;
            q.Each((ref FireTornadoEntity.Component _, ref Position position) =>
            {
                if (ranOnce) { return; }
                ranOnce = true;
                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        requestId = NetworkMechanic.TeaFireTornadoAttackShanoa.ToString(),
                        mechanicId = (uint)NetworkMechanic.TeaFireTornadoAttackShanoa,
                        worldPositionX = position.Value.X,
                        worldPositionY = position.Value.Y,
                        worldPositionZ = position.Value.Z,
                        rotation = default(float),
                    }
                }).SafeFireAndForget();
            });
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == ViceAndVirtueActionId)
        {
            fireTornado.SafeDestruct();
        }

        else if (set.Action.Value.RowId == AetherCompassActionId &&
            availableGuidanceMarkers.Count > 0 &&
            set.Source.GameObjectId == Dalamud.ObjectTable.LocalPlayer?.GameObjectId)
        {
            int closestMarker = -1;
            float closestMarkerDistance = float.PositiveInfinity;
            Vector3 markerPosition = default;
            unsafe
            {
                var markers = MarkingController.Instance()->FieldMarkers;
                for (var i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    if (marker.Active && availableGuidanceMarkers.Contains(i))
                    {
                        var distance = Vector2.Distance(set.Source.Position.ToVector2(), marker.Position.ToVector2());
                        if (distance <= GuidanceMarkerRadius && distance < closestMarkerDistance)
                        {
                            closestMarker = i;
                            closestMarkerDistance = distance;
                            markerPosition = marker.Position;
                            markerPosition.Y = arenaMiddle.Y; // in case markers are illegally placed
                        }
                    }
                }
            }

            if (closestMarker >= 0)
            {
                // calculate target rotation
                float rotation = 0;
                if (shanoa.IsValid() && shanoa.TryGet(out Position shanoaPosition))
                {
                    rotation = MathUtilities.VectorToRotation((markerPosition - shanoaPosition.Value).ToVector2());
                }

                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        requestId = System.Guid.NewGuid().ToString(),
                        mechanicId = (uint)NetworkMechanic.TeaMoveShanoa,
                        worldPositionX = markerPosition.X,
                        worldPositionY = markerPosition.Y,
                        worldPositionZ = markerPosition.Z,
                        rotation = rotation,
                        extraData = closestMarker.ToString(),
                    }
                }).SafeFireAndForget();
            }
        }
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaShowShanoa:
                {
                    this.shanoa.SafeDestruct();
                    if (EntityManager.TryCreateEntity<Shanoa>(out var shanoa))
                    {
                        shanoa
                            .Set(new Position(new(payload.worldPositionX ?? default, payload.worldPositionY ?? default, payload.worldPositionZ ?? default)))
                            .Set(new Rotation(payload.rotation ?? default))
                            .Set(new ChatBubble("Meow!♪"));
                        attacks.Add(shanoa);
                        this.shanoa = shanoa;
                    }
                }
                break;

            case (int)NetworkMechanicCommand.TeaShowShanoaGuidanceMarkers:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 2) { return; }
                    if (!byte.TryParse(arguments[0], out var availableMarkersFlags)) { return; }
                    if (!float.TryParse(arguments[1], out var duration)) { return; }
                    ShowGuidanceMarkers(availableMarkersFlags, duration);
                }
                break;

            case (int)NetworkMechanicCommand.TeaMoveShanoa:
                {
                    ClearGuidanceEntities();
                    if (!shanoa.IsValid()) { return; }
                    shanoa
                        .Set(new Shanoa.TargetPosition(new Vector3(
                            payload.worldPositionX ?? default,
                            payload.worldPositionY ?? default,
                            payload.worldPositionZ ?? default)))
                        .Set(new Shanoa.TargetRotation(payload.rotation ?? default))
                        .Set(new ChatBubble("Meow! Purrrrrr...♪"));
                    if (!string.IsNullOrEmpty(payload.extraData))
                    {
                        var arguments = payload.extraData.Split(',');
                        if (float.TryParse(arguments[0], out var movementSpeed) &&
                            float.TryParse(arguments[1], out var rotationSpeed))
                        {
                            shanoa.Set(new Shanoa.Component(movementSpeed, rotationSpeed));
                        }
                    }
                }
                break;

            case (int)NetworkMechanicCommand.TeaFireTornadoAttackShanoa:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 2) { return; }
                    if (!float.TryParse(arguments[0], out var omenDuration)) { return; }
                    if (!float.TryParse(arguments[1], out var distanceThreshold)) { return; }
                    if (!shanoa.IsValid()) { return; }
                    using var q = World.Query<FireTornadoEntity.Component>();
                    var fireTornado = q.First();
                    if (!fireTornado.IsValid()) { return; }

                    if (shanoa.TryGet(out Model shanoaModel) && fireTornado.TryGet(out Model fireTornadoModel))
                    {
                        var tether = World.Entity().Set(new TetherOmen.ProximityTether(
                            DistanceThreshold: distanceThreshold,
                            Source: fireTornadoModel.GameObject, Target: shanoaModel.GameObject))
                            .ChildOf(fireTornado);
                        attacks.Add(tether);

                        var action1 = DelayedAction.Create(World, () =>
                        {
                            tether.SafeDestruct();
                            if (fireTornado.IsValid())
                            {
                                World.Entity()
                                    .Set(new ActorVfx("vfx/monster/m0729/eff/m0729_sp01c0t2.avfx")) // PLACEHOLDER
                                    .Set(new ActorVfxTarget(shanoaModel.GameObject))
                                    .ChildOf(fireTornado);

                                var action2 = DelayedAction.Create(World, () =>
                                {
                                    if (shanoa.IsValid())
                                    {
                                        World.Entity()
                                            .Set(new ActorVfx("vfx/monster/m0729/eff/m0729_sp01t0t2.avfx")) // PLACEHOLDER
                                            .ChildOf(shanoa);
                                    }
                                }, 0.35f /*PLACEHOLDER*/);
                                attacks.Add(action2);
                            }
                        }, omenDuration);
                        attacks.Add(action1);
                    }
                }
                break;
        }
    }

    public override void DebugSimulate()
    {
        AttackMarkers();
    }

    private unsafe void AttackMarkers()
    {
        foreach (var marker in MarkingController.Instance()->FieldMarkers)
        {
            if (marker.Active)
            {
                var position = marker.Position;
                position.Y = arenaMiddle.Y; // in case markers are illegally placed

                var circleAttack = Circle.CreateEntity(World)
                    .Set(new Position(position))
                    .Set(new Scale(5.0f * Vector3.One))
                    .Set(new Circle.Component(2.8f, 0.3f, MarkerAttackVfxPath, 0.3f, (e) =>
                    {
                        var player = Dalamud.ObjectTable.LocalPlayer;
                        if (player == null || player.IsDead) { return; }
                        if (player.HasTranscendance())
                        {
                            VfxSpawn.PlayInvulnerabilityEffect(player);
                        }
                        else
                        {
                            Stun.ApplyToTarget(e, 10.0f);
                        }
                    }));
                attacks.Add(circleAttack);

                var action = DelayedAction.Create(World, () =>
                {
                    NetworkClient.SendAsync(new Message
                    {
                        action = Message.Action.StartMechanic,
                        startMechanic = new Message.StartMechanicPayload
                        {
                            requestId = NetworkMechanic.TeaShowShanoaGuidanceMarkers.ToString() + "_protean1",
                            mechanicId = (uint)NetworkMechanic.TeaShowShanoaGuidanceMarkers,
                        }
                    }).SafeFireAndForget();
                }, 3.75f);
                attacks.Add(action);
            }
        }
    }

    private unsafe void ShowGuidanceMarkers(byte availableMarkersFlags, float duration)
    {
        ClearGuidanceEntities();

        var markers = MarkingController.Instance()->FieldMarkers;
        Logger.Info("availableMarkersFlags:{0}, markers.Length:{1}", availableMarkersFlags, markers.Length);
        for (var i = 0; i < 8; i++)
        {
            if (i > markers.Length) { continue; }
            var marker = markers[i];
            if (!marker.Active) { continue; }

            if ((availableMarkersFlags & (1 << i)) != 0)
            {
                var position = marker.Position;
                position.Y = arenaMiddle.Y; // in case markers are illegally placed

                var ring = World.Entity()
                    .Set(new StaticVfx(AetherCompassLocationVfxPath))
                    .Set(new Position(position))
                    .Set(new Rotation())
                    .Set(new Scale(GuidanceMarkerRadius * 0.1f * Vector3.One))
                    .Add<Components.Omen>();
                attacks.Add(ring);
                guidanceEntities.Add(ring);

                var arrows = World.Entity()
                    .Set(new StaticVfx(AetherCompassLocationArrowsVfxPath))
                    .Set(new Position(position))
                    .Set(new Rotation())
                    .Set(new Scale(0.75f * Vector3.One))
                    .Add<Components.Omen>();
                attacks.Add(arrows);
                guidanceEntities.Add(arrows);

                availableGuidanceMarkers.Add(i);
            }
        }

        if (guidanceEntities.Count > 0)
        {
            var action = DelayedAction.Create(World, ClearGuidanceEntities, duration);
            attacks.Add(action);
            guidanceEntities.Add(action);

            var hint = "Shanoa responds to the Aether Compass!";
            var raptureAtkModule = RaptureAtkModule.Instance();
            if (raptureAtkModule != null)
            {
                raptureAtkModule->ShowTextGimmickHint(hint, RaptureAtkModule.TextGimmickHintStyle.Warning, 4 * 10);
            }
            Dalamud.ChatGui.PrintSystemMessage(hint);
        }
    }

    private void ClearGuidanceEntities()
    {
        foreach (var e in guidanceEntities)
        {
            e.SafeDestruct();
        }
        guidanceEntities.Clear();
        availableGuidanceMarkers.Clear();
    }
}
