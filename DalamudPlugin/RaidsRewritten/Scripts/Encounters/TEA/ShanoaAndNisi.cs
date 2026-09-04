using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.MathHelpers;
using Flecs.NET.Core;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaAndNisi : Mechanic
{
    public int RngSeed { get; set; }
    public IShanoaPark? ShanoaPark { get; set; }

    private const int SendNisiUpdateIntervalMs = 1000;
    private const uint NisiAlphaStatusId = 2222;
    private const uint NisiBetaStatusId = 2223;
    private const uint NisiGammaStatusId = 2137;
    private const uint NisiDeltaStatusId = 2138;
    private const uint LiquidRageDataId = 0x2C49;
    private const uint EyeOfTheChakramActionId = 18517;
    private const uint PhotonActionId = 18486;
    private const uint MissileCommandActionId = 18509;
    private const uint SludgePuddleDataId = 0x1E958C;
    private const uint IcePuddleDataId = 0x1E958D;
    private const uint FlarethrowerActionId = 18501;
    private const uint LimitCutActionId = 18483;
    private const uint WhirlwindActionId = 18882;
    private const uint GavelActionId = 18492;
    private const uint PropellerWindActionId = 18482;

    private const float TowerDistanceFromMiddle = 15.0f;

    public struct NisiVfx;

    private readonly List<Entity> attacks = [];
    private readonly IReadOnlyList<Vector3> cardinalTornadoPositions =
        [new(85, 0, 100),
        new(115, 0, 100),
        new(100, 0, 85),
        new(100, 0, 115)];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    private DateTime nextAllowedUpdateNisiSend;
    private NisiTowerOmen.Nisi lastNisiPolled;
    private bool inBjccPhase;
    private bool eyeOfTheChakramCasted;
    private bool photonCasted;
    //private bool sludgePuddleCreated = false;
    private int nisiTowersSpawned = 0;
    private List<Vector3> towerPositions = [];
    private int towerPositionsIndex = 0;
    private List<NisiTowerOmen.Nisi> availableNisis = [NisiTowerOmen.Nisi.Alpha, NisiTowerOmen.Nisi.Beta, NisiTowerOmen.Nisi.Gamma, NisiTowerOmen.Nisi.Delta];
    private int nisiIndex = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
        lastNisiPolled = NisiTowerOmen.Nisi.None;
        inBjccPhase = false;
        eyeOfTheChakramCasted = false;
        photonCasted = false;
        //sludgePuddleCreated = false;
        nisiTowersSpawned = 0;
        towerPositions = [];
        towerPositionsIndex = 0;
        availableNisis = [NisiTowerOmen.Nisi.Alpha, NisiTowerOmen.Nisi.Beta, NisiTowerOmen.Nisi.Gamma, NisiTowerOmen.Nisi.Delta];
        nisiIndex = 0;
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
        var seed = RngSeed;
        unchecked
        {
            seed += nisiTowersSpawned * 0xB151;
        }
        var random = new Random(seed);
        availableNisis = availableNisis.AsValueEnumerable().OrderBy(_ => random.Next()).ToList();
        towerPositionsIndex = random.Next(4);
    }

    public override void OnCombatEnd()
    {
        Reset();
    }

    public override void OnFrameworkUpdate(IFramework framework)
    {
        if (!NetworkClient.IsConnected) { return; }
        if (!inBjccPhase) { return; }

        var player = Dalamud.ObjectTable.LocalPlayer;
        if (player == null) { return; }

        var nisi = NisiTowerOmen.Nisi.None;
        foreach (var status in player.StatusList)
        {
            switch (status.StatusId)
            {
                case NisiAlphaStatusId: nisi = NisiTowerOmen.Nisi.Alpha; break;
                case NisiBetaStatusId: nisi = NisiTowerOmen.Nisi.Beta; break;
                case NisiGammaStatusId: nisi = NisiTowerOmen.Nisi.Gamma; break;
                case NisiDeltaStatusId: nisi = NisiTowerOmen.Nisi.Delta; break;
            }
            if (nisi != NisiTowerOmen.Nisi.None)
            {
                break;
            }
        }

        var currentTime = Dalamud.Framework.LastUpdateUTC;

        if (nisi != lastNisiPolled || currentTime >= nextAllowedUpdateNisiSend)
        {
            nextAllowedUpdateNisiSend = currentTime.AddMilliseconds(SendNisiUpdateIntervalMs);
            SendUpdateNisi(nisi);
        }

        lastNisiPolled = nisi;
    }

    public override void OnMapEffect(uint Position, ushort Param1, ushort Param2)
    {
        if (Position == 7 && Param1 == 4 && Param2 == 2)
        {
            inBjccPhase = true;
        }
        else
        {
            if (inBjccPhase)
            {
                SendUpdateNisi(NisiTowerOmen.Nisi.None);
            }
            inBjccPhase = false;
        }
    }

    public override void OnObjectCreation(nint newObjectPointer, IGameObject? newObject)
    {
        if (newObject == null) { return; }

        if (newObject.BaseId == LiquidRageDataId)
        {
            if (towerPositions.Count == 0)
            {
                float[] angles;
                if (cardinalTornadoPositions.Contains(newObject.Position))
                {
                    angles = [-0.75f * MathF.PI, -0.25f * MathF.PI, 0.25f * MathF.PI, 0.75f * MathF.PI];
                }
                else
                {
                    angles = [-MathF.PI, -0.5f * MathF.PI, 0, 0.5f * MathF.PI];
                }
                towerPositions = angles.AsValueEnumerable()
                    .Select(r => (TowerDistanceFromMiddle * MathUtilities.RotationToUnitVector(r)).ToVector3(0) + arenaMiddle)
                    .ToList();
            }

            else if (nisiTowersSpawned == 4)
            {
                ShanoaPark?.AttackMarkers("water");
            }
        }

        //else if (newObject.BaseId == SludgePuddleDataId)
        //{
        //    if (sludgePuddleCreated) { return; }
        //    sludgePuddleCreated = true;

        //    ShanoaPark?.AttackMarkers("sludge");
        //}

        else if (newObject.BaseId == IcePuddleDataId)
        {
            SendSpawnNextPositionNisiTower();
        }
    }

    public override void OnStartingCast(Lumina.Excel.Sheets.Action action, IBattleChara source)
    { 
        if (action.RowId == WhirlwindActionId)
        {
            if (nisiTowersSpawned == 3)
            {
                SendSpawnNextPositionNisiTower();
            }
        }

        else if (action.RowId == GavelActionId)
        {
            SendSpawnNextPositionNisiTower();
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == EyeOfTheChakramActionId)
        {
            if (eyeOfTheChakramCasted) { return; }
            eyeOfTheChakramCasted = true;

            var action1 = DelayedAction.Create(World, () =>
            {
                var nisiTowerPosition = towerPositions.SafeSelect(towerPositionsIndex);
                var rotation = MathUtilities.VectorToRotation((nisiTowerPosition - arenaMiddle).ToVector2());
                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        requestId = NetworkMechanic.TeaShowShanoa + "_photon",
                        mechanicId = (uint)NetworkMechanic.TeaShowShanoa,
                        worldPositionX = arenaMiddle.X,
                        worldPositionY = arenaMiddle.Y,
                        worldPositionZ = arenaMiddle.Z,
                        rotation = rotation,
                        extraData = "1",
                    }
                }).SafeFireAndForget();

                SendSpawnNisiTower(arenaMiddle);
            }, 1.0f);
            attacks.Add(action1);
        }

        else if (set.Action.Value.RowId == PhotonActionId)
        {
            if (photonCasted) { return; }
            photonCasted = true;

            ShanoaPark?.AttackMarkers("photon");

            var action2 = DelayedAction.Create(World, () =>
            {
                SendSpawnNextPositionNisiTower();
            }, 2.0f);
            attacks.Add(action2);
        }

        else if (set.Action.Value.RowId == MissileCommandActionId)
        {
            var action = DelayedAction.Create(World, () =>
            {
                ShanoaPark?.AttackMarkers("missilecommand");
            }, 1.0f);
            attacks.Add(action);
        }

        else if (set.Action.Value.RowId == FlarethrowerActionId)
        {
            var action = DelayedAction.Create(World, () =>
            {
                ShanoaPark?.AttackMarkers("flarethrower");
            }, 1.0f);
            attacks.Add(action);
        }
    }

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaUpdateShanoaNisiStatus:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    if (!byte.TryParse(payload.extraData, out var rawNisi)) { return; }
                    var nisi = (NisiTowerOmen.Nisi)rawNisi;
                    using var q = World.Query<Shanoa.Component>();
                    q.Each((Entity e, ref Shanoa.Component shanoa) =>
                    {
                        string vfxPath = string.Empty;
                        switch (nisi)
                        {
                            case NisiTowerOmen.Nisi.None:
                                World.Defer(() =>
                                {
                                    e.DestructChildEntity<NisiVfx>();
                                });
                                return;
                            case NisiTowerOmen.Nisi.Alpha:
                                vfxPath = NisiTowerOmen.NisiAlphaVfxPath; break;
                            case NisiTowerOmen.Nisi.Beta:
                                vfxPath = NisiTowerOmen.NisiBetaVfxPath; break;
                            case NisiTowerOmen.Nisi.Gamma:
                                vfxPath = NisiTowerOmen.NisiGammaVfxPath; break;
                            case NisiTowerOmen.Nisi.Delta:
                                vfxPath = NisiTowerOmen.NisiDeltaVfxPath; break;
                            default:
                                return;
                        }
                        World.Entity()
                            .Set(new ActorVfx(vfxPath))
                            .Add<NisiVfx>()
                            .ChildOf(e);
                    });
                }
                break;

            case (int)NetworkMechanicCommand.TeaUpdateNisiTower:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 3) { return; }
                    if (!BigInteger.TryParse(arguments[0], out var vfxId)) { return; }
                    if (!byte.TryParse(arguments[1], out var rawNisi)) { return; }
                    if (!int.TryParse(arguments[2], out var inTowerCount)) { return; }
                    var position = new Vector3(payload.worldPositionX ?? default, payload.worldPositionY ?? default, payload.worldPositionZ ?? default);
                    var rotation = payload.rotation ?? default;
                    var nisi = (NisiTowerOmen.Nisi)rawNisi;
                    using var q = World.Query<VfxId>();
                    var vfx = q.Find((ref VfxId v) => v.Value == vfxId);
                    if (!vfx.IsValid())
                    {
                        if (EntityManager.TryCreateEntity<NisiTowerOmen>(out var tower))
                        {
                            tower.Set(new Scale(Vector3.One));
                            tower.Set(new VfxId(vfxId));
                            tower.Set(new NisiTowerOmen.Component(nisi));
                            attacks.Add(tower);
                            vfx = tower;
                        }
                        else
                        {
                            return;
                        }
                    }
                    vfx.Set(new Position(position));
                    vfx.Set(new Rotation(rotation));
                    vfx.Set(new InTowerOmen(inTowerCount));
                }
                break;
        }
    }

    private void SendUpdateNisi(NisiTowerOmen.Nisi nisi)
    {
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                requestId = Guid.NewGuid().ToString(),
                mechanicId = (uint)NetworkMechanic.TeaUpdateNisiStatus,
                extraData = ((byte)nisi).ToString(),
            }
        }).SafeFireAndForget();
    }

    private void SendSpawnNextPositionNisiTower()
    {
        if (towerPositions.Count == 0)
        {
            towerPositionsIndex = 0;
            return;
        }
        if (towerPositionsIndex >= towerPositions.Count)
        {
            towerPositionsIndex = towerPositions.Count - 1;
        }
        var position = towerPositions[towerPositionsIndex++];
        towerPositionsIndex %= towerPositions.Count;
        SendSpawnNisiTower(position);
    }

    private void SendSpawnNisiTower(Vector3 position)
    {
        var nisi = availableNisis[nisiIndex++];
        nisiIndex %= availableNisis.Count;

        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                requestId = NetworkMechanic.TeaNisiTower + $"_{nisiTowersSpawned}",
                mechanicId = (uint)NetworkMechanic.TeaNisiTower,
                worldPositionX = position.X,
                worldPositionY = position.Y,
                worldPositionZ = position.Z,
                rotation = 0,
                extraData = ((byte)nisi).ToString(),
            }
        }).SafeFireAndForget();

        nisiTowersSpawned++;
    }
}
