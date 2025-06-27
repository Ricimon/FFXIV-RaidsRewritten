using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RaidsRewritten.Extensions;
using RaidsRewritten.Log;
using SocketIOClient;

namespace RaidsRewritten.Network;

public class ServerConnection : IDisposable
{
    /// <summary>
    /// When in a public room, this plugin will automatically switch rooms when the player changes maps.
    /// This property indicates if the player should be connected to a public room.
    /// </summary>
    public bool ShouldBeInRoom { get; private set; }
    public bool InRoom { get; private set; }

    public IEnumerable<string> PlayersInRoom
    {
        get
        {
            if (InRoom)
            {
                if (this.playersInRoom == null)
                {
                    return [this.localPlayerFullName ?? "null"];
                }
                else
                {
                    return this.playersInRoom;
                }
            }
            else
            {
                return [];
            }
        }
    }

    public ServerConnectionChannel? Channel { get; private set; }

    private const string PeerType = "player";

    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IChatGui chatGui;
    private readonly MapManager mapManager;
    private readonly ILogger logger;

    private readonly LoadConfig loadConfig;

    private string? localPlayerFullName;
    private string[]? playersInRoom;

    public ServerConnection(
        IDalamudPluginInterface pluginInterface,
        IClientState clientState,
        IObjectTable objectTable,
        IFramework framework,
        IChatGui chatGui,
        MapManager mapManager,
        ILogger logger)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.framework = framework;
        this.chatGui = chatGui;
        this.mapManager = mapManager;
        this.logger = logger;

        var configPath = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "config.json");
        this.loadConfig = null!;
        if (File.Exists(configPath))
        {
            var configString = File.ReadAllText(configPath);
            try
            {
                this.loadConfig = JsonSerializer.Deserialize<LoadConfig>(configString)!;
            }
            catch (Exception) { }
        }
        if (this.loadConfig == null)
        {
            logger.Warn("Could not load config file at {0}", configPath);
            this.loadConfig = new();
        }

        this.clientState.Logout += OnLogout;
    }

    public void Dispose()
    {
        this.Channel?.Dispose();
        this.clientState.Logout -= OnLogout;
        GC.SuppressFinalize(this);
    }

    public void JoinPublicRoom()
    {
        if (this.ShouldBeInRoom)
        {
            this.logger.Error("Already should be in room, ignoring public room join request.");
            return;
        }
        string roomName = this.mapManager.GetCurrentMapPublicRoomName();
        string[]? otherPlayers = this.mapManager.InSharedWorldMap() ? null : GetOtherPlayerNamesInInstance().ToArray();
        JoinRoom(roomName, string.Empty, otherPlayers);
        this.mapManager.OnMapChanged += ReconnectToCurrentMapPublicRoom;
    }

    public void JoinPrivateRoom(string roomName, string roomPassword)
    {
        if (this.ShouldBeInRoom)
        {
            this.logger.Error("Already should be in room, ignoring private room join request.");
            return;
        }
        JoinRoom(roomName, roomPassword, null);
    }

    public Task LeaveRoom(bool autoRejoin)
    {
        if (!autoRejoin)
        {
            this.ShouldBeInRoom = false;
            this.mapManager.OnMapChanged -= ReconnectToCurrentMapPublicRoom;
        }

        if (!this.InRoom)
        {
            return Task.CompletedTask;
        }

        this.logger.Debug("Attempting to leave room.");

        this.InRoom = false;
        this.localPlayerFullName = null;
        this.playersInRoom = null;

        //if (this.configuration.PlayRoomJoinAndLeaveSounds)
        //{
        //    this.audioDeviceController.PlaySfx(this.roomSelfLeaveSound)
        //        .ContinueWith(task => this.audioDeviceController.AudioPlaybackIsRequested = false, TaskContinuationOptions.OnlyOnRanToCompletion)
        //        .SafeFireAndForget(ex =>
        //        {
        //            if (ex is not TaskCanceledException) { this.logger.Error(ex.ToString()); }
        //        });
        //}
        //else
        //{
        //    this.audioDeviceController.AudioPlaybackIsRequested = false;
        //}

        if (this.Channel != null)
        {
            //this.Channel.OnConnected -= OnSignalingServerConnected;
            //this.Channel.OnReady -= OnSignalingServerReady;
            //this.Channel.OnDisconnected -= OnSignalingServerDisconnected;
            //this.Channel.OnErrored -= OnSignalingServerDisconnected;
            this.Channel.OnMessage -= OnMessage;
            return this.Channel.DisconnectAsync();
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    private void OnLogout(int type, int code)
    {
        LeaveRoom(false);
    }

    private IEnumerable<string> GetOtherPlayerNamesInInstance()
    {
        return this.objectTable.GetPlayers()
            .Select(p => p.GetPlayerFullName())
            .Where(s => s != null)
            .Where(s => s != this.clientState.GetLocalPlayerFullName())
            .Cast<string>();
    }

    private void JoinRoom(string roomName, string roomPassword, string[]? playersInInstance)
    {
        if (this.InRoom)
        {
            this.logger.Error("Already in room, ignoring join request.");
            return;
        }

        this.logger.Debug("Attemping to join room.");

        var playerName = this.clientState.GetLocalPlayerFullName();
        if (playerName == null)
        {
#if DEBUG
            playerName = "testPeer14";
            this.logger.Warn("Player name is null. Setting it to {0} for debugging.", playerName);
#else
            this.logger.Error("Player name is null, cannot join voice room.");
            return;
#endif
        }

        this.InRoom = true;
        this.ShouldBeInRoom = true;
        this.localPlayerFullName = playerName;

        this.logger.Trace("Creating ServerConnectionChannel class with peerId {0}", playerName);
        if (this.Channel == null)
        {
            this.Channel = new ServerConnectionChannel(playerName,
                PeerType,
                this.loadConfig.serverUrl,
                this.loadConfig.serverToken,
                this.logger,
                true);
        }
        else
        {
            this.Channel.PeerId = playerName;
        }

        this.Channel.OnMessage += OnMessage;

        this.logger.Debug("Attempting to connect to server.");
        this.Channel.ConnectAsync(roomName, roomPassword, playersInInstance).SafeFireAndForget(ex =>
        {
            if (ex is not OperationCanceledException)
            {
                this.logger.Error(ex.ToString());
            }
        });
    }

    private void ReconnectToCurrentMapPublicRoom()
    {
        if (this.ShouldBeInRoom &&
            (!this.InRoom || this.Channel?.RoomName != this.mapManager.GetCurrentMapPublicRoomName()))
        {
            Task.Run(async () =>
            {
                await this.LeaveRoom(true);
                // Add an arbitrary delay here as loading a new map can result in a null local player name during load.
                // This delay hopefully allows the game to populate that field before a reconnection attempt happens.
                // Also in some housing districts, the mapId is different after the OnTerritoryChanged event
                await Task.Delay(1000);
                // Accessing the object table must happen on the main thread
                this.framework.Run(() =>
                {
                    var roomName = this.mapManager.GetCurrentMapPublicRoomName();
                    string[]? otherPlayers = this.mapManager.InSharedWorldMap() ? null : GetOtherPlayerNamesInInstance().ToArray();
                    this.JoinRoom(roomName, string.Empty, otherPlayers);
                }).SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
            });
        }
    }

    private void OnMessage(SocketIOResponse response)
    {
        ServerMessage message;
        ServerMessage.Payload payload;
        try
        {
            message = response.GetValue<ServerMessage>();
            payload = message.payload;
        }
        catch (Exception e)
        {
            this.logger.Error(e.ToString());
            return;
        }

        switch (payload.action)
        {
            case ServerMessage.Payload.Action.UpdatePlayersInRoom:
                this.playersInRoom = payload.players;
                break;
        }
    }
}
