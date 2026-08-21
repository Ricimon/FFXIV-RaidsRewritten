using System.Collections.Generic;
using System.Numerics;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using Lumina.Excel.Sheets;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Scripts.Attacks.Omens;
using RaidsRewritten.Scripts.Components;
using RaidsRewritten.Scripts.Models;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.E1S;

public class ShanoaAndFireTornadoTest : Mechanic
{
    private const uint EdensGravityActionId = 15728;
    private const uint ViceAndVirtueActionId = 17647;
    public static readonly Vector3 FireTornadoPosition = new(87.0f, 0.0f, 113.0f);

    private readonly List<Entity> attacks = [];
    private Entity fireTornado;
    private bool edensGravityCasted = false;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.SafeDestruct();
        }
        attacks.Clear();
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
        if (EntityManager.TryCreateEntity<FireTornadoEntity>(out var tornado))
        {
            tornado.Set(new Position(FireTornadoPosition));
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
