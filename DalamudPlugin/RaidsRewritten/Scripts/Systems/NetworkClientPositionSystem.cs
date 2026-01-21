using System;
using AsyncAwaitBestPractices;
using Flecs.NET.Core;
using RaidsRewritten.Game;
using RaidsRewritten.Network;

namespace RaidsRewritten.Scripts.Systems;

public class NetworkClientPositionSystem(DalamudServices dalamud, NetworkClient networkClient) : ISystem
{
    private const int SendIntervalMs = 250;

    private DateTime nextAllowedPositionSend;

    public void Register(World world)
    {
        world.System()
            .Each((_, _) =>
            {
                if (!networkClient.IsConnected) { return; }

                var player = dalamud.ObjectTable.LocalPlayer;
                if (player == null) { return; }

                var currentTime = dalamud.Framework.LastUpdateUTC;
                if (currentTime < nextAllowedPositionSend) { return; }

                nextAllowedPositionSend = currentTime.AddMilliseconds(SendIntervalMs);
                networkClient.SendAsync(new Message
                {
                    action = Message.Action.UpdateStatus,
                    updateStatus = new Message.UpdateStatusPayload
                    {
                        worldPositionX = player.Position.X,
                        worldPositionY = player.Position.Y,
                        worldPositionZ = player.Position.Z,
                    }
                }).SafeFireAndForget();
            });
    }
}
