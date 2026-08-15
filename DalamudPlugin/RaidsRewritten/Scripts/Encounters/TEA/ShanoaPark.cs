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

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaPark : Mechanic
{
    private const uint LiquidRageDataId = 0x2C49;
    private const uint FluidSwingActionId = 18864;
    private const uint LivingLiquidProteanWaveActionId = 18468;
    private const uint AetherCompassActionId = 26988;

    private const string MarkerAttackVfxPath = "vfx/monster/d1024/eff/arthur_thunderstorm_t0s.avfx";
    private const string AetherCompassLocationVfxPath = "bg/ex2/05_zon_z3/common/vfx/eff/b1526bari1_u.avfx";
    private const string AetherCompassLocationArrowsVfxPath = "bgcommon/world/common/vfx_for_bg/eff/b1490tagt1_o.avfx";

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
    private HashSet<int> availableGuidanceMarkers = [];

    private HashSet<Vector3> availableTornadoPositions = [];
    private Entity Shanoa;
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
        availableGuidanceMarkers.Clear();
        fluidSwingsPerformed = 0;
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
                        mechanicId = (int)NetworkMechanic.TeaSpawnShanoa,
                        worldPositionX = openTornadoPosition.X,
                        worldPositionY = openTornadoPosition.Y,
                        worldPositionZ = openTornadoPosition.Z,
                        rotation = default(float),
                    }
                }).SafeFireAndForget();
            }
        }

        else if (set.Action.Value.RowId == AetherCompassActionId &&
            availableGuidanceMarkers.Count > 0 &&
            set.Source.GameObjectId == Dalamud.ObjectTable.LocalPlayer?.GameObjectId)
        {
            unsafe
            {
                var markers = MarkingController.Instance()->FieldMarkers;
                int closestMarker = -1;
                float closestMarkerDistance = float.PositiveInfinity;
                for (var i = 0; i < markers.Length; i++)
                {
                    var marker = markers[i];
                    if (marker.Active && availableGuidanceMarkers.Contains(i))
                    {
                        var distance = Vector2.Distance(set.Source.Position.ToVector2(), marker.Position.ToVector2());
                        if (distance < closestMarkerDistance)
                        {
                            closestMarker = i;
                            closestMarkerDistance = distance;
                        }
                    }
                }

                if (closestMarker >= 0)
                {
                    NetworkClient.SendAsync(new Message
                    {
                        action = Message.Action.StartMechanic,
                        startMechanic = new Message.StartMechanicPayload
                        {
                            requestId = System.Guid.NewGuid().ToString(),
                            mechanicId = (uint)NetworkMechanic.TeaMoveShanoa,
                            extraData = closestMarker.ToString(),
                        }
                    }).SafeFireAndForget();
                }
            }
        }
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaShowShanoa:
                {
                    Shanoa.SafeDestruct();
                    if (EntityManager.TryCreateEntity<Shanoa>(out Shanoa))
                    {
                        Shanoa
                            .Set(new Position(new(payload.worldPositionX ?? default, payload.worldPositionY ?? default, payload.worldPositionZ ?? default)))
                            .Set(new Rotation(payload.rotation ?? default))
                            .Set(new ChatBubble("Meow!♪"));
                        attacks.Add(Shanoa);
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
                    if (!Shanoa.IsValid()) { return; }
                    Shanoa
                        .Set(new Shanoa.TargetPosition(new Vector3(
                            payload.worldPositionX ?? default,
                            payload.worldPositionY ?? default,
                            payload.worldPositionZ ?? default)))
                        .Set(new Shanoa.TargetRotation(payload.rotation ?? default));
                    if (!string.IsNullOrEmpty(payload.extraData))
                    {
                        var arguments = payload.extraData.Split(',');
                        if (float.TryParse(arguments[0], out var movementSpeed) &&
                            float.TryParse(arguments[1], out var rotationSpeed))
                        {
                            Shanoa.Set(new Shanoa.Component(movementSpeed, rotationSpeed));
                        }
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
                var circleAttack = Circle.CreateEntity(World)
                    .Set(new Position(marker.Position))
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
                            requestId = NetworkMechanic.TeaShowShanoaGuidanceMarkers.ToString() + "protean1",
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
        for (var i = 0; i < 8; i++)
        {
            if (i > markers.Length) { return; }
            var marker = markers[i];
            if (!marker.Active) { return; }

            if ((availableMarkersFlags & (1 << i)) != 0)
            {
                var ring = World.Entity()
                    .Set(new StaticVfx(AetherCompassLocationVfxPath))
                    .Set(new Position(marker.Position))
                    .Set(new Rotation())
                    .Set(new Scale(GuidanceMarkerRadius * 0.1f * Vector3.One))
                    .Add<Components.Omen>();
                attacks.Add(ring);
                guidanceEntities.Add(ring);

                var arrows = World.Entity()
                    .Set(new StaticVfx(AetherCompassLocationArrowsVfxPath))
                    .Set(new Position(marker.Position))
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
