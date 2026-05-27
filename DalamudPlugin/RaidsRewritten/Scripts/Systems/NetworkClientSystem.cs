using System;
using System.Collections.Generic;
using AsyncAwaitBestPractices;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Log;
using RaidsRewritten.Network;
using RaidsRewritten.Scripts.Conditions;
using RaidsRewritten.Utility;

namespace RaidsRewritten.Scripts.Systems;

public sealed class NetworkClientSystem(DalamudServices dalamud, NetworkClient networkClient, Configuration configuration, ILogger logger) : ISystem, IDisposable
{
    private const int SendPositionIntervalMs = 250;

    private struct SyncConditions;

    private Query<Condition.Component, Condition.Id, Condition.NetworkMessage> localPlayerClientConditions;
    private Query<Condition.Component, Condition.Id, Condition.NetworkMessage> localPlayerUnsyncedClientConditions;
    private long? syncedPartyId;
    private DateTime nextAllowedPositionSend;

    public void Register(World world)
    {
        localPlayerClientConditions = world.QueryBuilder<Condition.Component, Condition.Id, Condition.NetworkMessage>()
            .With<Player.LocalPlayer>().Up()
            .Without<Condition.ServerCondition>().Cached().Build();
        localPlayerUnsyncedClientConditions = world.QueryBuilder<Condition.Component, Condition.Id, Condition.NetworkMessage>()
            .With<Player.LocalPlayer>().Up()
            .Without<Condition.ServerCondition>().Without<Condition.SyncedToServer>().Cached().Build();

        world.System()
            .Each((_, _) =>
            {
                RunPartySync();
                RunPositionSync();
            });

        world.System<Condition.Component>()
            .With<Player.LocalPlayer>().Up()
            .Each((Iter it, int i, ref Condition.Component _) =>
            {
                var entity = it.Entity(i);
                if (!networkClient.IsConnected)
                {
                    entity.Remove<Condition.SyncedToServer>();
                    return;
                }
            });

        // Client-controlled condition syncing
        world.System()
            .With<Player.LocalPlayer>()
            .Each((Iter it, int i) =>
            {
                var entity = it.Entity(i);
                if (!networkClient.IsConnected)
                {
                    return;
                }

                if (localPlayerUnsyncedClientConditions.IsTrue() ||
                    entity.Has<SyncConditions>())
                {
                    var conditions = new List<Message.SyncConditionsOnSelf.ConditionDetails>();
                    localPlayerClientConditions.Each((Entity e, ref Condition.Component condition, ref Condition.Id id, ref Condition.NetworkMessage networkMessage) =>
                    {
                        conditions.Add(new Message.SyncConditionsOnSelf.ConditionDetails
                        {
                            id = id.Value,
                            condition = networkMessage.Condition,
                            timeRemaining = condition.TimeRemaining,
                            newlyApplied = !e.Has<Condition.SyncedToServer>(),
                        });
                        e.Add<Condition.SyncedToServer>();
                    });
                    var syncConditionsOnSelf = new Message
                    {
                        action = Message.Action.SyncConditionsOnSelf,
                        syncConditionsOnSelf = new Message.SyncConditionsOnSelf
                        {
                            conditions = [..conditions],
                        }
                    };
                    networkClient.SendAsync(syncConditionsOnSelf).SafeFireAndForget();

                    entity.Remove<SyncConditions>();
                }
            });

        // Broadcast conditions to server when any client-controlled conditions are removed
        // Trying to use With/Without methods on an OnRemove observer causes a crash on Dispose, not sure why
        world.Observer<Condition.Component>()
            .Event(Ecs.OnRemove)
            .Each((Entity e, ref Condition.Component _) =>
            {
                if (!networkClient.IsConnected) { return; }

                if (!e.IsValid()) { return; }
                if (e.Has<Condition.ServerCondition>()) { return; }

                var parent = e.Parent();
                if (!parent.IsValid()) { return; }
                if (!parent.Has<Player.LocalPlayer>()) { return; }

                parent.Add<SyncConditions>();
            });
    }

    public void Dispose()
    {
        if (localPlayerClientConditions.IsValid()) { localPlayerClientConditions.Dispose(); }
        if (localPlayerUnsyncedClientConditions.IsValid()) { localPlayerUnsyncedClientConditions.Dispose(); }
    }

    private void RunPartySync()
    {
        if (!networkClient.IsConnected ||
            !dalamud.PlayerState.IsLoaded)
        {
            syncedPartyId = null;
            return;
        }

        if (syncedPartyId.HasValue)
        {
            if (configuration.UseCustomPartyId ||
                syncedPartyId == dalamud.PartyList.PartyId)
            {
                return;
            }
        }

        var updatePlayer = new Message
        {
            action = Message.Action.UpdatePlayer,
            updatePlayer = new Message.UpdatePlayerPayload
            {
                contentId = dalamud.PlayerState.ContentId,
                name = dalamud.PlayerState.CharacterName,
                role = GetRole(),
                party = configuration.UseCustomPartyId ? configuration.CustomPartyId : CalculatePartyHash(),
            },
        };
        networkClient.SendAsync(updatePlayer).SafeFireAndForget();
        syncedPartyId = dalamud.PartyList.PartyId;
    }

    private void RunPositionSync()
    {
        if (!networkClient.IsConnected) { return; }

        var player = dalamud.ObjectTable.LocalPlayer;
        if (player == null) { return; }

        var currentTime = dalamud.Framework.LastUpdateUTC;
        if (currentTime < nextAllowedPositionSend) { return; }

        nextAllowedPositionSend = currentTime.AddMilliseconds(SendPositionIntervalMs);
        networkClient.SendAsync(new Message
        {
            action = Message.Action.UpdateStatus,
            updateStatus = new Message.UpdateStatusPayload
            {
                worldPositionX = player.Position.X,
                worldPositionY = player.Position.Y,
                worldPositionZ = player.Position.Z,
                isAlive = !player.IsDead,
            }
        }).SafeFireAndForget();
    }

    private Message.UpdatePlayerPayload.Role GetRole()
    {
        return dalamud.PlayerState.ClassJob.Value.JobType switch
        {
            1 => Message.UpdatePlayerPayload.Role.Tank,
            2 or 6 => Message.UpdatePlayerPayload.Role.Healer,
            3 or 4 or 5 => Message.UpdatePlayerPayload.Role.Dps,
            _ => Message.UpdatePlayerPayload.Role.None,
        };
    }

    // Adapted from https://git.anna.lgbt/anna/RightThere/src/commit/f6ebe5271d90fd11680480fd27f05e0154dd0ef2/client/RpcClient.cs#L37
    private string CalculatePartyHash()
    {
        var id = dalamud.PartyList.PartyId;
        var bytes = BitConverter.GetBytes(id);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        using var hasher = Blake3.Hasher.New();
        hasher.Update("RaidsRewritten party"u8);
        hasher.Update(bytes);
        var hash = hasher.Finalize();

        return Convert.ToBase64String(hash.AsSpan());
    }
}
