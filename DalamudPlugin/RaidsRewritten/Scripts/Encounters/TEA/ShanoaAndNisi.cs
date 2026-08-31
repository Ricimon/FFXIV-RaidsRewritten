using System;
using System.Collections.Generic;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaAndNisi : Mechanic
{
    public int RngSeed { get; set; }

    private const int SendNisiUpdateIntervalMs = 1000;
    private const uint NisiAlphaStatusId = 2222;
    private const uint NisiBetaStatusId = 2223;
    private const uint NisiGammaStatusId = 2137;
    private const uint NisiDeltaStatusId = 2138;
    private const uint PhotonActionId = 18486;

    public struct NisiVfx;

    private readonly List<Entity> attacks = [];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    private DateTime nextAllowedUpdateNisiSend;
    private NisiTowerOmen.Nisi lastNisiPolled;
    private bool inBjccPhase;
    private bool photonCasted;
    private int nisiTowersSpawned = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
        lastNisiPolled = NisiTowerOmen.Nisi.None;
        inBjccPhase = false;
        photonCasted = false;
        nisiTowersSpawned = 0;
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null || set.Source == null) { return; }

        if (set.Action.Value.RowId == PhotonActionId)
        {
            if (photonCasted) { return; }
            photonCasted = true;

            DelayedAction.Create(World, () =>
            {
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
                        rotation = 0,
                        extraData = "1",
                    }
                }).SafeFireAndForget();

                SendSpawnNisiTower(arenaMiddle);
            }, 1.0f);
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

    private void SendSpawnNisiTower(Vector3 position)
    {
        var seed = RngSeed;
        unchecked
        {
            seed += nisiTowersSpawned * 0xB151;
        }
        var random = new Random(seed);
        var nisi = (NisiTowerOmen.Nisi)random.Next((int)NisiTowerOmen.Nisi.Alpha, (int)NisiTowerOmen.Nisi.Delta + 1);
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
                extraData = nisi.ToString(),
            }
        }).SafeFireAndForget();

        nisiTowersSpawned++;
    }
}
