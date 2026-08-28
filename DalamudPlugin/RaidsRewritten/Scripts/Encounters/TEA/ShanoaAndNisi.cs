using System;
using System.Collections.Generic;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Plugin.Services;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaAndNisi : Mechanic
{
    private const int SendNisiUpdateIntervalMs = 1000;
    private const uint NisiAlphaStatusId = 2222;
    private const uint NisiBetaStatusId = 2223;
    private const uint NisiGammaStatusId = 2137;
    private const uint NisiDeltaStatusId = 2138;
    private const string NisiAlphaVfxPath = "vfx/common/eff/m0598_stlp6c0c.avfx";
    private const string NisiBetaVfxPath = "vfx/common/eff/m0598_stlp7c0c.avfx";
    private const string NisiGammaVfxPath = "vfx/common/eff/m0598_stlp8c0c.avfx";
    private const string NisiDeltaVfxPath = "vfx/common/eff/m0598_stlp9c0c.avfx";
    private const uint PhotonActionId = 18486;

    private enum Nisi : byte
    {
        None = 0,
        Alpha = 1,
        Beta = 2,
        Gamma = 3,
        Delta = 4,
    }

    public struct NisiVfx;

    private readonly List<Entity> attacks = [];
    private readonly Vector3 arenaMiddle = new(100, 0, 100);

    private DateTime nextAllowedUpdateNisiSend;
    private Nisi lastNisiPolled;
    private bool inBjccPhase;
    private bool photonCasted;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
        lastNisiPolled = Nisi.None;
        inBjccPhase = false;
        photonCasted = false;
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

        var nisi = Nisi.None;
        foreach (var status in player.StatusList)
        {
            switch (status.StatusId)
            {
                case NisiAlphaStatusId: nisi = Nisi.Alpha; break;
                case NisiBetaStatusId: nisi = Nisi.Beta; break;
                case NisiGammaStatusId: nisi = Nisi.Gamma; break;
                case NisiDeltaStatusId: nisi = Nisi.Delta; break;
            }
            if (nisi != Nisi.None)
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
                SendUpdateNisi(Nisi.None);
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
                    var nisi = (Nisi)rawNisi;
                    using var q = World.Query<Shanoa.Component>();
                    q.Each((Entity e, ref Shanoa.Component shanoa) =>
                    {
                        string vfxPath = string.Empty;
                        switch (nisi)
                        {
                            case Nisi.None:
                                World.Defer(() =>
                                {
                                    e.DestructChildEntity<NisiVfx>();
                                });
                                return;
                            case Nisi.Alpha:
                                vfxPath = NisiAlphaVfxPath; break;
                            case Nisi.Beta:
                                vfxPath = NisiBetaVfxPath; break;
                            case Nisi.Gamma:
                                vfxPath = NisiGammaVfxPath; break;
                            case Nisi.Delta:
                                vfxPath = NisiDeltaVfxPath; break;
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
        }
    }

    private void SendUpdateNisi(Nisi nisi)
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
}
