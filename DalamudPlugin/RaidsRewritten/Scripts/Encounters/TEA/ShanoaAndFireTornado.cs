using System.Collections.Generic;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class ShanoaAndFireTornado : Mechanic
{
    private const uint LivingLiquidProteanWaveActionId = 18468;

    private readonly List<Entity> attacks = [];

    private bool protean1Casted = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
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

    public override void OnStartingCast(Action action, IBattleChara source)
    {
        if (!protean1Casted && action.RowId == LivingLiquidProteanWaveActionId)
        {
            protean1Casted = true;

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

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaFireTornadoAttackShanoa:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 2) { return; }
                    if (!float.TryParse(arguments[0], out var omenDuration)) { return; }
                    if (!float.TryParse(arguments[1], out var distanceThreshold)) { return; }

                    using var q1 = World.Query<Shanoa.Component>();
                    var shanoa = q1.First();
                    if (!shanoa.IsValid()) { return; }
                    using var q2 = World.Query<FireTornadoEntity.Component>();
                    var fireTornado = q2.First();
                    if (!fireTornado.IsValid()) { return; }

                    if (shanoa.TryGet(out Model shanoaModel) && fireTornado.TryGet(out Model fireTornadoModel))
                    {
                        var fireTornadoGo = Dalamud.ObjectTable.GetGameObjectByIndex(fireTornadoModel.ObjectIndex);
                        var shanoaGo = Dalamud.ObjectTable.GetGameObjectByIndex(shanoaModel.ObjectIndex);
                        var tether = World.Entity().Set(new TetherOmen.ProximityTether(
                            DistanceThreshold: distanceThreshold,
                            Source: fireTornadoGo, Target: shanoaGo))
                            .ChildOf(fireTornado);
                        attacks.Add(tether);

                        var action1 = DelayedAction.Create(World, () =>
                        {
                            tether.SafeDestruct();
                            if (fireTornado.IsValid() && shanoa.IsValid())
                            {
                                World.Entity()
                                    .Set(new ActorVfx("vfx/monster/gimmick4/eff/w5d1_bb_g02c0c.avfx"))
                                    .Set(new ActorVfxTarget(shanoaGo))
                                    .ChildOf(fireTornado);
                            }
                        }, omenDuration);
                        attacks.Add(action1);
                    }
                }
                break;
        }
    }
}
