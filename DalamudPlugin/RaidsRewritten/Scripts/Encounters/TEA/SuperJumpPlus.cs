using System.Collections.Generic;
using AsyncAwaitBestPractices;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class SuperJumpPlus : Mechanic
{
    private const uint SuperJumpActionId = 18506;

    private readonly List<Entity> attacks = [];

    private int superJumpCount = 0;

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        superJumpCount = 0;
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
        if (set.Action.Value.RowId == SuperJumpActionId)
        {
            superJumpCount++;

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

                //targets = [Dalamud.PlayerState.ContentId];
                if (targets.Count > 0)
                {
                    var action = DelayedAction.Create(World, () =>
                    {
                        NetworkClient.SendAsync(new Message
                        {
                            action = Message.Action.StartMechanic,
                            startMechanic = new Message.StartMechanicPayload
                            {
                                requestId = "SuperJump_" + superJumpCount,
                                mechanicId = (uint)NetworkMechanic.TeaSuperJumpEnumeration,
                                extraData = string.Join(',', targets),
                            }
                        }).SafeFireAndForget();
                    }, 0.75f);
                    attacks.Add(action);
                }
            }
        }
    }
}
