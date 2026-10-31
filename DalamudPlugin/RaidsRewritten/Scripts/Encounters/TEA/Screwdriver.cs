using System;
using System.Collections.Generic;
using AsyncAwaitBestPractices;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using ECommons.Hooks;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Attacks;
using RaidsRewritten.Utility;
using ZLinq;

namespace RaidsRewritten.Scripts.Encounters.TEA;

public class Screwdriver : Mechanic
{
    public int RngSeed { get; set; }

    private record struct Target(Entity Entity, ulong ContentId);

    private readonly List<Entity> attacks = [];
    private readonly HashSet<ulong> alreadyAssignedTargets = [];

    public override void Reset()
    {
        foreach (var attack in attacks)
        {
            attack.Destruct();
        }
        attacks.Clear();
        alreadyAssignedTargets.Clear();
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

    public override void OnNetworkMechanicCommand(Message.RunMechanicCommandPayload payload)
    {
        switch (payload.mechanicCommandId)
        {
            case (int)NetworkMechanicCommand.TeaUpdateScrewdriverState:
                {
                    if (string.IsNullOrEmpty(payload.extraData)) { return; }
                    var arguments = payload.extraData.Split(',');
                    if (arguments.Length < 4) { return; }
                    if (!ulong.TryParse(arguments[0], out var targetContentId)) { return; }
                    if (!int.TryParse(arguments[1], out var direction)) { return; }
                    if (!int.TryParse(arguments[2], out var rotationState)) { return; }
                    if (!ulong.TryParse(arguments[3], out var linkedToContentId)) { return; }
                    // rotationState
                    // 3 = 3-bar
                    // 2 = 2-bar
                    // 1 = 1-bar
                    // 0 = success
                    // -1 = fail

                    // First try to find the Screw attack
                    using var q1 = World.Query<Screw.Component>();
                    var screwAttack = q1.Find((ref Screw.Component component) =>
                    {
                        if (component.Target is ICharacter character)
                        {
                            return character.GetContentId() == targetContentId;
                        }
                        return false;
                    });

                    // Create the attack if it doesn't yet exist
                    if (!screwAttack.IsValid())
                    {
                        using var q2 = World.Query<Player.Component, Player.ContentId>();
                        var target = q2.Find((ref pc, ref contentId) => pc.PlayerCharacter != null && contentId.Value == targetContentId);
                        if (target.IsValid() && target.TryGet(out Player.Component pc) &&
                            EntityManager.TryCreateEntity<Screw>(out Entity screw))
                        {
                            screw.Set(new Screw.Component(true, (Screw.Direction)direction, pc.PlayerCharacter!,
                                UpdateRotationCount, Resolve));
                            screwAttack = screw;
                        }
                    }

                    if (screwAttack.IsValid())
                    {
                        screwAttack.Set(new Screw.ServerState(rotationState, rotationState == 0, rotationState == -1, linkedToContentId));
                    }
                    break;
                }
        }
    }

    public override void DebugSimulate()
    {
        StartMechanic();
    }

    private void StartMechanic()
    {
        var tanks = new List<Target>();
        var healers = new List<Target>();
        var dps = new List<Target>();
        var allTargets = new List<Target>();
        CommonQueries.AllPlayersQuery.Each((Entity e, ref Player.Component player) =>
        {
            if (player.PlayerCharacter == null || player.PlayerCharacter.IsDead) { return; }
            if (!e.TryGet(out Player.ContentId contentId)) { return; }

            allTargets.Add(new(e, contentId.Value));
            List<Target>? list = null;
            switch (player.PlayerCharacter.GetRole())
            {
                case CombatRole.Tank:
                    list = tanks; break;
                case CombatRole.Healer:
                    list = healers; break;
                case CombatRole.DPS:
                    list = dps; break;
                default:
                    return;
            }

            list.Add(new(e, contentId.Value));
        });

        var seed = RngSeed;
        unchecked
        {
            seed += 0x5C6E1111 + alreadyAssignedTargets.Count;
        }
        var random = new Random(RngSeed);

        List<List<Target>> lists = [tanks, healers, dps, allTargets];
        for (var i = 0; i < lists.Count; i++)
        {
            lists[i] = [.. lists[i].AsValueEnumerable()
                .OrderBy(v => v.ContentId)
                .OrderBy(v => random.Next())
                .OrderBy(v => alreadyAssignedTargets.Contains(v.ContentId))];
        }

        // Assign targets
        var assignedTargets = new List<Target>();

        var numTanks = 1;
        var numHealers = 1;
        var numDps = 2;

        foreach (var t in tanks)
        {
            if (numTanks > 0)
            {
                assignedTargets.Add(t);
                numTanks--;
            }
        }
        foreach (var t in healers)
        {
            if (numHealers > 0)
            {
                assignedTargets.Add(t);
                numHealers--;
            }
        }
        foreach (var t in dps)
        {
            if (numDps > 0)
            {
                assignedTargets.Add(t);
                numDps--;
            }
        }

        var numTargetsLeft = numTanks + numHealers + numDps;
        if (numTargetsLeft > 0)
        {
            allTargets.RemoveAll(t => assignedTargets.AsValueEnumerable().Any(at => t.ContentId == at.ContentId));
            foreach (var t in allTargets)
            {
                if (numTargetsLeft > 0)
                {
                    assignedTargets.Add(t);
                    numTargetsLeft--;
                }
            }
        }

        // Apply mechanic to assigned targets
        var j = alreadyAssignedTargets.Count;
        foreach (var t in assignedTargets)
        {
            j++;
            var direction = random.Next(2);
            NetworkClient.SendAsync(new Message
            {
                action = Message.Action.StartMechanic,
                startMechanic = new Message.StartMechanicPayload
                {
                    mechanicId = (uint)NetworkMechanic.TeaScrewdriverTarget,
                    requestId = nameof(NetworkMechanic.TeaScrewdriverTarget) + $"_{j}",
                    extraData = string.Join(",", [t.ContentId, direction]),
                }
            }).SafeFireAndForget();
        }

        // For later runs of this mechanic
        foreach (var t in assignedTargets)
        {
            alreadyAssignedTargets.Add(t.ContentId);
        }
    }

    private void UpdateRotationCount(Screw.Component component, int rotationCount)
    {
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                mechanicId = (uint)NetworkMechanic.TeaScrewdriverTarget,
                requestId = nameof(NetworkMechanic.TeaScrewdriverTarget) + $"_{Guid.NewGuid()}",
                extraData = string.Join(",", [component.Target.GetContentId(), (int)component.Direction, rotationCount]),
            }
        }).SafeFireAndForget();
    }

    private void Resolve(Screw.Component component, bool success)
    {
        NetworkClient.SendAsync(new Message
        {
            action = Message.Action.StartMechanic,
            startMechanic = new Message.StartMechanicPayload
            {
                mechanicId = (uint)NetworkMechanic.TeaScrewdriverTarget,
                requestId = nameof(NetworkMechanic.TeaScrewdriverTarget) + $"_{Guid.NewGuid()}",
                extraData = string.Join(",", [component.Target.GetContentId(), (int)component.Direction, success ? 0 : -1]),
            }
        }).SafeFireAndForget();
    }
}
