using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
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

    private HashSet<Vector3> availableTornadoPositions = [];
    private int fluidSwingsPerformed = 0;
    private bool protean1Casted = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        availableTornadoPositions.Clear();
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

        else if (set.Action.Value.RowId == AetherCompassActionId)
        {

        }
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        if (payload.mechanicCommandId == (int)NetworkMechanicCommand.TeaShowShanoa)
        {
            if (EntityManager.TryCreateEntity<Shanoa>(out var shanoa))
            {
                shanoa
                    .Set(new Position(new(payload.worldPositionX ?? default, payload.worldPositionY ?? default, payload.worldPositionZ ?? default)))
                    .Set(new Rotation(payload.rotation ?? default))
                    .Set(new ChatBubble("Meow!♪"));
                attacks.Add(shanoa);
            }
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
                    var ring = World.Entity()
                        .Set(new StaticVfx(AetherCompassLocationVfxPath))
                        .Set(new Position(marker.Position))
                        .Set(new Rotation())
                        .Set(new Scale(0.14f * Vector3.One))
                        .Add<Components.Omen>();
                    attacks.Add(ring);

                    var arrows = World.Entity()
                        .Set(new StaticVfx(AetherCompassLocationArrowsVfxPath))
                        .Set(new Position(marker.Position))
                        .Set(new Rotation())
                        .Set(new Scale(0.75f * Vector3.One))
                        .Add<Components.Omen>();
                    attacks.Add(arrows);
                }, 3.75f);
                attacks.Add(action);
            }
        }
    }
}
