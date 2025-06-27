using AsyncAwaitBestPractices;
using RaidsRewritten.Log;
using SocketIO.Serializer.SystemTextJson;
using SocketIOClient;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RaidsRewritten.Network;

public class ServerConnectionChannel : IDisposable
{
    // Since Dalamud 12, for some reason accessing socket parameters such as socket.Connected from the UI thread
    // would crash the game. So, intermediate field booleans are now used to indicate state to the UI.
    public bool Connected => this.socket != null && !this.connecting && this.disconnectCts != null && !this.disconnectCts.IsCancellationRequested;
    public bool Connecting => this.socket != null && this.connecting && this.disconnectCts != null && !this.disconnectCts.IsCancellationRequested;
    public string PeerId { get; set; }
    public string PeerType { get; }
    public string ServerUrl { get; }
    public string Token { get; }
    public string? RoomName { get; private set; }
    public string? LatestServerDisconnectMessage { get; private set; }

    public event Action? OnConnected;
    public event Action? OnReady;
    public event Action<SocketIOResponse>? OnMessage;
    public event Action? OnDisconnected;
    public event Action? OnErrored;

    private SocketIOClient.SocketIO? socket;
    private CancellationTokenSource? disconnectCts;
    private string? roomPassword;
    private string[]? playersInInstance;
    private bool connecting;
    // Connecting to an empty room may not send back a "Ready" reply, so don't rely on this for connection state
    private bool ready;

    private readonly ILogger logger;
    private readonly bool verbose;

    public ServerConnectionChannel(string peerId, string peerType, string serverUrl, string token, ILogger logger, bool verbose = false)
    {
        this.PeerId = peerId;
        this.PeerType = peerType;
        this.ServerUrl = serverUrl;
        this.Token = token;
        this.logger = logger;
        this.verbose = verbose;
    }

    public Task ConnectAsync(string roomName, string roomPassword, string[]? playersInInstance)
    {
        if (this.socket == null)
        {
            var socketOptions = new SocketIOOptions
            {
                Auth = new Dictionary<string, string>() { { "token", this.Token } },
                Reconnection = true,
            };

            var serverUrl = this.ServerUrl;
            // https://regex101.com/r/u8QBnU/2
            var pathMatch = Regex.Match(this.ServerUrl, @"(.+\/\/.[^\/]+)(.*)");
            if (pathMatch.Success && pathMatch.Groups.Count > 2)
            {
                serverUrl = pathMatch.Groups[1].Value;
                socketOptions.Path = pathMatch.Groups[2].Value;
            }

            this.socket = new SocketIOClient.SocketIO(serverUrl, socketOptions);
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                IncludeFields = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            this.socket.Serializer = new SystemTextJsonSerializer(options);
            this.AddListeners();
        }

        if (this.socket.Connected)
        {
            this.logger.Error("Server is already connected.");
            return Task.CompletedTask;
        }
        this.connecting = true;
        this.ready = false;
        this.disconnectCts?.Dispose();
        this.disconnectCts = new();
        this.RoomName = roomName;
        this.roomPassword = roomPassword;
        this.playersInInstance = playersInInstance;
        return this.socket.ConnectAsync(this.disconnectCts.Token);
    }

    public Task SendAsync(ServerMessage.Payload payload)
    {
        if (this.socket != null && this.socket.Connected)
        {
            return this.socket.EmitAsync("message", new ServerMessage
            {
                from = this.PeerId,
                target = "all",
                payload = payload,
            });
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public Task SendToAsync(string targetPeerId, ServerMessage.Payload payload)
    {
        if (this.socket != null && this.socket.Connected)
        {
            return this.socket.EmitAsync("messageOne", new ServerMessage
            {
                from = this.PeerId,
                target = targetPeerId,
                payload = payload,
            });
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public Task DisconnectAsync()
    {
        if (this.socket != null)
        {
            if (this.socket.Connected)
            {
                return this.socket.DisconnectAsync();
            }
            else
            {
                this.logger.Debug("Cancelling server connection.");
                this.disconnectCts?.Cancel();
                this.DisposeSocket();
                return Task.CompletedTask;
            }
        }
        else
        {
            return Task.CompletedTask;
        }
    }

    public void ClearLatestDisconnectMessage()
    {
        this.LatestServerDisconnectMessage = null;
    }

    public void Dispose()
    {
        this.OnConnected = null;
        this.OnReady = null;
        this.OnMessage = null;
        this.OnDisconnected = null;
        this.DisposeSocket();
        GC.SuppressFinalize(this);
    }

    private void AddListeners()
    {
        if (this.socket != null)
        {
            this.socket.OnConnected += this.OnConnect;
            this.socket.OnDisconnected += this.OnDisconnect;
            this.socket.OnError += this.OnError;
            this.socket.OnReconnected += this.OnReconnect;
            this.socket.On("message", this.OnMessageCallback);
            this.socket.On("serverDisconnect", this.OnServerDisconnect);
        }
    }

    private void DisposeSocket()
    {
        if (this.socket != null)
        {
            this.socket.OnConnected -= this.OnConnect;
            this.socket.OnDisconnected -= this.OnDisconnect;
            this.socket.OnError -= this.OnError;
            this.socket.OnReconnected -= this.OnReconnect;
            this.socket.Off("message");
            this.socket.Off("serverDisconnect");
            this.socket.Dispose();
        }
        this.socket = null;
    }

    private void OnConnect(object? sender, EventArgs args)
    {
        try
        {
            if (this.socket == null || !this.socket.Connected)
            {
                return;
            }

            if (this.verbose)
            {
                this.logger.Debug("Connected to server.");
            }
            this.connecting = false;
            this.OnConnected?.Invoke();
            this.socket.EmitAsync("ready", this.PeerId, this.PeerType, this.RoomName, this.roomPassword, this.playersInInstance)
                .SafeFireAndForget(ex => this.logger.Error(ex.ToString()));
        }
        catch (Exception ex)
        {
            this.logger.Error(ex.ToString());
        }
    }

    private void OnDisconnect(object? sender, string reason)
    {
        try
        {
            if (this.verbose)
            {
                this.logger.Debug("Disconnected from server, reason: {0}", reason);
            }
            this.OnDisconnected?.Invoke();
            this.DisposeSocket();
        }
        catch (Exception ex)
        {
            this.logger.Error(ex.ToString());
        }
    }

    private void OnError(object? sender, string error)
    {
        this.logger.Error("Server ERROR: " + error);
        // An errored socket is considered disconnected, but we'll need to manually set disconnection state
        // and cancel the connection attempt.
        this.disconnectCts?.Cancel();
        this.disconnectCts?.Dispose();
        this.disconnectCts = null;
        this.OnErrored?.Invoke();
        // There's a known exception here when attempting to connect again, due to the strange way
        // the Socket.IO for .NET library internally handles Task state transitions.
        // But it's avoidable if we dispose the socket entirely
        this.DisposeSocket();
    }

    private void OnReconnect(object? sender, int attempts)
    {
        if (this.verbose)
        {
            this.logger.Info("Server reconnect, attempts: {0}", attempts);
        }
    }

    private void OnMessageCallback(SocketIOResponse response)
    {
        //if (this.verbose)
        //{
        //    this.logger.Trace("Server message: {0}", response);
        //}
        this.OnMessage?.Invoke(response);
        // Assume that any message callback implies readiness
        if (!this.ready)
        {
            this.ready = true;
            this.OnReady?.Invoke();
        }
    }

    private void OnServerDisconnect(SocketIOResponse response)
    {
        this.LatestServerDisconnectMessage = response.GetValue<ServerDisconnectMessage>().message;
        this.logger.Error("Server disconnect: {0}", response);

        // DEPRECATED COMMENT:
        // This message auto disconnects the client, but does not immediately set the socket state to not Connected.
        // So we need to dispose and nullify the token to avoid calling Cancel on the token, which for some reason
        // throws an exception due to cancellation token subscriptions.
        this.disconnectCts?.Dispose();
        this.disconnectCts = null;
        this.DisposeSocket();

        // Since Dalamud 12, this message no longer auto disconnects the client.
        this.OnDisconnected?.Invoke();
    }
}
