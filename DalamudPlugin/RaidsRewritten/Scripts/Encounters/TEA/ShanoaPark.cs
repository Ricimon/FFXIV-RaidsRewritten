using System.Collections.Generic;
using System.Linq;
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
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Spawn;
using RaidsRewritten.Utility;
using static RaidsRewritten.Scripts.Encounters.TEA.ShanoaAndNisi;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaPark : Mechanic
{
    private const uint LiquidRageDataId = 0x2C49;
    private const uint FluidSwingActionId = 18864;
    private const uint LivingLiquidProteanWaveActionId = 18468;
    private const uint AetherCompassActionId = 26988;
    private const uint LivingLiquidBaseId = 0x2C47;
    private const uint CascadeActionId = 18470;

    private const string MarkerAttackVfxPath = "vfx/monster/d1024/eff/arthur_thunderstorm_t0s.avfx";
    private const string AetherCompassLocationVfxPath = "bg/ex2/05_zon_z3/common/vfx/eff/b1526bari1_u.avfx";
    private const string AetherCompassLocationArrowsVfxPath = "bgcommon/world/common/vfx_for_bg/eff/b1490tagt1_o.avfx";
    private const string AbsorbMarkerVfxPath1 = "vfx/monster/m0982/eff/m0982sp006c0c.avfx";
    private const string AbsorbMarkerVfxPath2 = "vfx/monster/m0982/eff/m0982sp006t0c.avfx";
    private const string FinalSentenceVfxPath = "vfx/monster/gimmick/eff/alexfour_hitogata_shinpan_c0c.avfx";
    private const string FinalSentenceSfxPath = "sound/vfx/monster3/SE_Vfx_Monster_QuadrupedMachine_Judgement_c.scd";

    private const string ShanoaAetherCompassMessage = "Shanoa responds to the Aether Compass!";
    private const string ShanoaRunsAwayMessage = "Shanoa runs off...";
    private const string ShanoaAbsorbMarkerMessage = "Shanoa absorbs the waymark's essence.";

    private const float GuidanceMarkerRadius = 1.4f;

    private readonly List<Entity> attacks = [];
    private readonly IReadOnlyList<Vector3> tornadoPositions1 =
        [new(85, 0, 100),
        new(115, 0, 100),
        new(100, 0, 85),
        new(100, 0, 115)];
    private readonly IReadOnlyList<Vector3> tornadoPositions2 =
        [new(89.3934f, 0, 110.6066f),
        new(110.6066f, 0, 110.6066f),
        new(110.6066f, 0, 89.39339f),
        new(89.39339f, 0, 89.3934f)];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);
    private readonly List<Entity> guidanceEntities = [];
    private readonly HashSet<int> availableGuidanceMarkers = [];

    private HashSet<Vector3> availableTornadoPositions = [];
    private Entity shanoa;
    private int fluidSwingsPerformed = 0;
    private bool protean1Casted = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
        ClearGuidanceEntities();
        availableTornadoPositions.Clear();
        fluidSwingsPerformed = 0;
        protean1Casted = false;
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory a3)
    {
        if (a3 == DirectorUpdateCategory.Wipe ||
            a3 == DirectorUpdateCategory.Recommence)
        {
            Reset();
        }
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }
        if (newObject.BaseId != LiquidRageDataId) { return; }
        if (availableTornadoPositions.Count == 1) { return; }

        if (availableTornadoPositions.Count == 0)
        {
            if (tornadoPositions1.Contains(newObject.Position))
            {
                availableTornadoPositions = [.. tornadoPositions1];
            }
            else
            {
                availableTornadoPositions = [.. tornadoPositions2];
            }
        }

        availableTornadoPositions.Remove(newObject.Position);
    }

    public override void OnStartingCast(Action action, IBattleChara source)
    {
        if (!protean1Casted && action.RowId == LivingLiquidProteanWaveActionId)
        {
            protean1Casted = true;
            AttackMarkers();
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == FluidSwingActionId)
        {
            fluidSwingsPerformed++;
            if (fluidSwingsPerformed == 3 && availableTornadoPositions.Count == 1)
            {
                var openTornadoPosition = availableTornadoPositions.Single();
                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        requestId = NetworkMechanic.TeaSpawnShanoa.ToString(),
                        mechanicId = (uint)NetworkMechanic.TeaSpawnShanoa,
                        worldPositionX = openTornadoPosition.X,
                        worldPositionY = openTornadoPosition.Y,
                        worldPositionZ = openTornadoPosition.Z,
                        rotation = default(float),
                    }
                }).SafeFireAndForget();
            }
        }

        else if (set.Action.Value.RowId == CascadeActionId)
        {
            if (shanoa.IsValid())
            {
                shanoa.Set(new OneTimeModelTimeline(Shanoa.StretchAnimationId));
            }
            var action = DelayedAction.Create(World, () =>
            {
                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        mechanicId = (uint)NetworkMechanic.TeaHideShanoa,
                        requestId = nameof(NetworkMechanic.TeaHideShanoa) + "_Cascade",
                    }
                }).SafeFireAndForget();
            }, 1.0f);
            attacks.Add(action);
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

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        if (source.BaseId == LivingLiquidBaseId &&
            command == 14) // command 14 seems to be death animation
        {
            shanoa.SafeDestruct();
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
                        if (arguments.Length >= 2 &&
                            float.TryParse(arguments[0], out var movementSpeed) &&
                            float.TryParse(arguments[1], out var rotationSpeed))
                        {
                            shanoa.Set(new Shanoa.Component(movementSpeed, rotationSpeed));
                        }
                    }
                }
                break;

            case (int)NetworkMechanicCommand.TeaShanoaRunsAway:
                {
                    var movementSpeed = 6.0f;
                    var rotationSpeed = 7.0f;
                    if (!string.IsNullOrEmpty(payload.extraData))
                    {
                        var arguments = payload.extraData.Split(',');
                        if (arguments.Length >= 2)
                        {
                            _ = float.TryParse(arguments[0], out movementSpeed);
                            _ = float.TryParse(arguments[1], out rotationSpeed);
                        }
                    }

                    var delay = 0.0f;
                    using (var q = World.Query<FireTornadoEntity.Component>())
                    {
                        if (q.IsTrue())
                        {
                            // Assume the fire tornado attacked Shanoa; this needs a bit of delay on the animation
                            delay = 0.9f;
                        }
                    }
                    if (delay == 0.0f)
                    {
                        using var q = World.QueryBuilder<Shanoa.Component>().With<NisiVfx>().TermAt(0).Up().Build();
                        q.Each((Entity e, ref Shanoa.Component _) =>
                        {
                            delay = 0.75f;
                            var shanoaEntity = e.Parent();
                            World.Entity()
                                .Set(new ActorVfx(FinalSentenceVfxPath))
                                .ChildOf(shanoaEntity);
                            ResourceLoader.PlaySound(FinalSentenceSfxPath, 0);
                            DelayedAction.Create(World, () =>
                            {
                                e.SafeDestruct();
                            }, 0.75f);
                        });
                    }
                    var action = DelayedAction.Create(World, () =>
                    {
                        var shanoaFound = false;
                        using var q = World.Query<Shanoa.Component, Model, Position, Rotation>();
                        q.Each((Entity entity, ref Shanoa.Component _, ref Model model, ref Position position, ref Rotation rotation) =>
                        {
                            shanoaFound = true;
                            var forward = MathUtilities.RotationToUnitVector(rotation.Value);
                            var targetPosition = position.Value + 1000.0f * forward.ToVector3(position.Value.Y);
                            entity
                                .Set(new Shanoa.TargetPosition(targetPosition))
                                .Set(new Shanoa.Component(movementSpeed, rotationSpeed))
                                .Set(new ModelFadeOut(model.ObjectIndex, 1.5f, 1.5f))
                                .Set(new ChatBubble("Mrow!!"));
                        });

                        if (shanoaFound)
                        {
                            Dalamud.ToastGui.ShowNormal(ShanoaRunsAwayMessage);
                            Dalamud.ChatGui.PrintSystemMessage(ShanoaRunsAwayMessage);
                        }
                    }, delay);
                    attacks.Add(action);
                }
                break;

            case (int)NetworkMechanicCommand.TeaShanoaAbsorbsMarker:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    if (!byte.TryParse(payload.extraData, out var markerId)) { return; }
                    if (!TryGetMarkerPosition(markerId, out var markerPosition)) { return; }
                    if (!shanoa.IsValid()) { return; }
                    if (shanoa.TryGet(out Model shanoaModel))
                    {
                        var shanoaGo = Dalamud.ObjectTable.GetGameObjectByIndex(shanoaModel.ObjectIndex);
                        FakeActor.Create(World)
                            .Set(new Position(markerPosition))
                            .Set(new ActorVfx(AbsorbMarkerVfxPath1))
                            .Set(new ActorVfxTarget(shanoaGo));
                        var action = DelayedAction.Create(World, () =>
                        {
                            if (shanoa.IsValid())
                            {
                                World.Entity().Set(new ActorVfx(AbsorbMarkerVfxPath2)).ChildOf(shanoa);
                            }
                        }, 0.4f);
                        attacks.Add(action);
                        shanoa.Set(new OneTimeModelTimeline(Shanoa.ScratchSelfAnimationId));

                        Dalamud.ToastGui.ShowNormal(ShanoaAbsorbMarkerMessage);
                        Dalamud.ChatGui.PrintSystemMessage(ShanoaAbsorbMarkerMessage);
                    }
                }
                break;

            case (int)NetworkMechanicCommand.TeaHideShanoa:
                {
                    shanoa.SafeDestruct();
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
        var markers = MarkingController.Instance()->FieldMarkers;
        for (var i = 0; i < markers.Length; i++)
        {
            var marker = markers[i];
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
            }
        }

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

    private unsafe void ShowGuidanceMarkers(byte availableMarkersFlags, float duration)
    {
        ClearGuidanceEntities();

        var markers = MarkingController.Instance()->FieldMarkers;
        for (var i = 0; i < 8; i++)
        {
            if (i >= markers.Length) { return; }
            var marker = markers[i];
            if (!marker.Active) { return; }

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

            var raptureAtkModule = RaptureAtkModule.Instance();
            if (raptureAtkModule != null)
            {
                raptureAtkModule->ShowTextGimmickHint(ShanoaAetherCompassMessage, RaptureAtkModule.TextGimmickHintStyle.Warning, 4 * 10);
            }
            Dalamud.ChatGui.PrintSystemMessage(ShanoaAetherCompassMessage);
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

    private unsafe bool TryGetMarkerPosition(byte markerId, out Vector3 position)
    {
        position = default;
        var markers = MarkingController.Instance()->FieldMarkers;
        if (markerId >= markers.Length)
        {
            return false;
        }
        var marker = markers[markerId];
        if (!marker.Active)
        {
            return false;
        }
        position = marker.Position;
        position.Y = arenaMiddle.Y;
        return true;
    }
}
