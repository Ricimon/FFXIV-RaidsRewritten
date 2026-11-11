using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using JsonConverters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RaidsRewritten.Data;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;
using SocketIOClient;
using SocketIOClient.Common;
using SocketIOClient.Serializer.NewtonsoftJson;

namespace RaidsRewritten.Network;

public sealed class NetworkClient : IDisposable
{
    public bool IsConnecting { get; private set; }
    public bool IsConnected { get; private set; }
    public byte ConnectedPlayersInParty { get; set; }

    public const string DefaultServerUrl = "http://localhost:3000";

    private readonly JsonSerializerSettings printSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new LongNameContractResolver(),
        Converters = [new StringEnumConverter()],
    };
    private readonly Lock clientLock = new();

    private readonly NetworkClientMessageHandler messageHandler;
    private readonly DalamudServices dalamud;
    private readonly Configuration configuration;
    private readonly Log.ILogger logger;
    private readonly DalamudLoggerProvider loggerProvider;

    private SocketIO? client;

    public NetworkClient(
        NetworkClientMessageHandler messageHandler,
        DalamudServices dalamud,
        Configuration configuration,
        Log.ILogger logger,
        DalamudLoggerProvider loggerProvider)
    {
        this.messageHandler = messageHandler;
        this.dalamud = dalamud;
        this.configuration = configuration;
        this.logger = logger;
        this.loggerProvider = loggerProvider;

        messageHandler.Client = this;
    }

    public string GetServerUrl()
    {
        var serverUrl = DefaultServerUrl;

        var configPath = dalamud.PluginInterface.GetResourcePath("config.json");
        if (File.Exists(configPath))
        {
            var configString = File.ReadAllText(configPath);
            try
            {
                var loadConfig = System.Text.Json.JsonSerializer.Deserialize<LoadConfig>(configString);
                if (loadConfig != null && !string.IsNullOrEmpty(loadConfig.serverUrl))
                {
                    serverUrl = loadConfig.serverUrl;
                }
            }
            catch (Exception) { }
        }

#if DEBUG
        if (!string.IsNullOrEmpty(configuration.ServerUrl))
        {
            serverUrl = configuration.ServerUrl;
        }
#endif

        return serverUrl;
    }

    public bool Connect()
    {
        lock (clientLock)
        {
            if (client != null || configuration.EverythingDisabled)
            {
                return false;
            }

            if (IsConnecting || IsConnected)
            {
                return true;
            }
        }

        try
        {
            var socketOptions = new SocketIOOptions()
            {
                Transport = TransportProtocol.WebSocket,
                Reconnection = true,
                ReconnectionAttempts = 3,
            };

            var serverUrl = GetServerUrl();
            // https://regex101.com/r/u8QBnU/2
            var pathMatch = Regex.Match(serverUrl, @"(.+\/\/.[^\/]+)(.*)");
            if (pathMatch.Success && pathMatch.Groups.Count > 2)
            {
                serverUrl = pathMatch.Groups[1].Value;
                var path = pathMatch.Groups[2].Value;
                if (!string.IsNullOrEmpty(path))
                {
                    socketOptions.Path = pathMatch.Groups[2].Value;
                }
            }

            lock (clientLock)
            {
                client = new SocketIO(new Uri(serverUrl), socketOptions, services =>
                {
                    services.AddNewtonsoftJson(new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        Converters = [new BooleanJsonConverter()],
                    });
#if DEBUG
                    services.AddLogging(builder =>
                    {
                        builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                        builder.AddProvider(loggerProvider);
                    });
#endif
                });
            }
        }
        catch(Exception e)
        {
            logger.Error(e.ToStringFull());
            return false;
        }

        client.OnAny((eventName, ctx) =>
        {
            try
            {
                var message = ctx.GetValue<Message>(0);
                logger.Info("Received socket message eventName {0}, message {1}", eventName, Serialize(message));
            }
            catch (Exception)
            {
                logger.Info("Received socket message eventName {0}, response {1}", eventName, ctx.GetValue<object>(0) ?? "null");
            }
            return Task.CompletedTask;
        });

        client.OnConnected += OnConnected;
        client.OnDisconnected += OnDisconnected;
        client.OnError += OnError;
        client.OnReconnectAttempt += OnReconnectAttempt;
        client.On("message", messageHandler.OnMessage);

        IsConnecting = true;
        client.ConnectAsync().SafeFireAndForget(_ => Dispose());

        return true;
    }

    public async Task SendAsync(Message message)
    {
        lock (clientLock)
        {
            if (client == null)
            {
                return;
            }
        }

        if (!IsConnected) { return; }
        if (message.action != Message.Action.UpdateStatus && message.action != Message.Action.UpdateFakePlayer)
        {
            logger.Info("Sending {0}", Serialize(message));
        }
        await client.EmitAsync("message", [message]);
    }

    public async Task DisconnectAsync()
    {
        lock (clientLock)
        {
            if (client == null)
            {
                return;
            }
        }

        logger.Info($"Disconnecting client.");
        client.DisconnectAsync().SafeFireAndForget();
        Dispose();
    }

    public void Dispose()
    {
        client?.DisconnectAsync().SafeFireAndForget();
        client?.Dispose();
        if (client != null)
        {
            client.OnConnected -= OnConnected;
            client.OnDisconnected -= OnDisconnected;
            client.OnError -= OnError;
            client.OnReconnectAttempt -= OnReconnectAttempt;
        }
        lock (clientLock)
        {
            client = null;
        }
        IsConnecting = IsConnected = false;
        ConnectedPlayersInParty = 0;
    }

    private string Serialize(object obj) => JsonConvert.SerializeObject(obj, printSettings);

    private void OnConnected(object? sender, EventArgs e)
    {
        lock (clientLock)
        {
            if (client == null) { return; }
        }

        logger.Info($"Client connected to {GetServerUrl()}");
        IsConnecting = false;
        IsConnected = true;

        messageHandler.OnConnect();

        if (!dalamud.PlayerState.IsLoaded)
        {
            logger.Warn("PlayerState is not loaded. Disconnecting client.");
            DisconnectAsync().SafeFireAndForget();
            return;
        }
    }

    private void OnDisconnected(object? sender, string e)
    {
        messageHandler.OnDisconnect();

        if (e == "io client disconnect")
        {
            logger.Info($"Client disconnected: {e}");
            Dispose();
        }
        else
        {
            logger.Warn($"Client disconnected: {e}, reconnecting...");
            Dispose();
            Connect();
        }
    }

    private void OnError(object? sender, string e)
    {
        logger.Error($"Client error: {e}");
        Dispose();
    }

    private void OnReconnectAttempt(object? sender, int e)
    {
        logger.Info($"Client ReconnectAttempt: {e}");
    }
}
