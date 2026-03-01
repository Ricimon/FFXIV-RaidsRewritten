using System;
using AsyncAwaitBestPractices;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;

namespace RaidsRewritten.Scripts.Systems;

public class NetworkClientSystem(DalamudServices dalamud, NetworkClient networkClient, Configuration configuration) : ISystem
{
    private const int SendPositionIntervalMs = 250;

    private long? syncedPartyId;
    private DateTime nextAllowedPositionSend;

    public void Register(World world)
    {
        world.System()
            .Each((_, _) =>
            {
                RunPartySync();
                RunPositionSync();
            });
    }

    private void RunPartySync()
    {
        if (!networkClient.IsConnected)
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
