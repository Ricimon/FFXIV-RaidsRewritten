using System.Collections.Generic;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class HawkTower : Mechanic
{
    private const uint CruiseChaserBaseId = 0x2C4E;
    private const uint SuperBlasstyChargeActionId = 19279;
    private const uint HawkBlasterActionId = 18480;

    private readonly List<Entity> attacks = [];

    private int blasstyChargeCount = 0;
    private int hawkBlasterCount = 0;
    private int ccCommandCount = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        blasstyChargeCount = 0;
        hawkBlasterCount = 0;
        ccCommandCount = 0;
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

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) { return; }
        if (set.Source == null) { return; }

        if (set.Action.Value.RowId == SuperBlasstyChargeActionId)
        {
            blasstyChargeCount++;

            List<ulong> targets = [];
            foreach (var target in set.TargetEffects)
            {
                var targetEntity = CommonQueries.AllPlayersQuery.Find((Entity e, ref Player.Component player) =>
                {
                    return player.PlayerCharacter?.GameObjectId == target.TargetID;
                });
                if (targetEntity.IsValid() && targetEntity.TryGet(out Player.ContentId contentId))
                {
                    targets.Add(contentId.Value);
                }
            }

            if (targets.Count > 0)
            {
                NetworkClient.SendAsync(new Message
                {
                    action = Message.Action.StartMechanic,
                    startMechanic = new Message.StartMechanicPayload
                    {
                        requestId = "BlasstyCharge_" + blasstyChargeCount,
                        mechanicId = (uint)NetworkMechanic.TeaBlasstyChargeHit,
                        extraData = string.Join(',', targets),
                    }
                }).SafeFireAndForget();
            }
        }

        else if (set.Action.Value.RowId == HawkBlasterActionId)
        {
            hawkBlasterCount++;

            if (hawkBlasterCount == 9 || hawkBlasterCount == 18)
            {
                var a = DelayedAction.Create(World, () =>
                {
                    NetworkClient.SendAsync(new Message
                    {
                        action = Message.Action.StartMechanic,
                        startMechanic = new Message.StartMechanicPayload
                        {
                            requestId = "HawkBlasterTower_" + hawkBlasterCount,
                            mechanicId = (uint)NetworkMechanic.TeaHawkBlasterTower,
                            worldPositionX = 100,
                            worldPositionY = 0,
                            worldPositionZ = 100,
                            rotation = 0,
                        }
                    }).SafeFireAndForget();
                }, 0.6f);
                attacks.Add(a);
            }
        }
    }

    public override void OnActorControl(IGameObject source, uint command, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, ulong targetId, byte replaying)
    {
        if (source.BaseId == CruiseChaserBaseId &&
            command == 62) // command 62 is when debuffs from Limit Cut are removed
        {
            ccCommandCount++;
            NetworkClient.SendAsync(new Message
            {
                action = Message.Action.StartMechanic,
                startMechanic = new Message.StartMechanicPayload
                {
                    requestId = "LimitCutEnd_" + ccCommandCount,
                    mechanicId = (uint)NetworkMechanic.TeaLimitCutEnd,
                }
            }).SafeFireAndForget();
        }
    }
}
