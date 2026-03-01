using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RaidsRewritten.Data;
using RaidsRewritten.Log;
using RaidsRewritten.Utility;
using SocketIO.Serializer.NewtonsoftJson;
using SocketIOClient;

namespace RaidsRewritten.Network;

public sealed class NetworkClient(
    NetworkClientMessageHandler messageHandler,
    DalamudServices dalamud,
    Configuration configuration,
    ILogger logger) : IDisposable
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

    private SocketIOClient.SocketIO? client;

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
        if (client != null || configuration.EverythingDisabled)
        {
            return false;
        }

        try
        {
            var socketOptions = new SocketIOOptions()
            {
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                ReconnectionAttempts = 3,
            };

            var serverUrl = GetServerUrl();
            // https://regex101.com/r/u8QBnU/2
            var pathMatch = Regex.Match(serverUrl, @"(.+\/\/.[^\/]+)(.*)");
            if (pathMatch.Success && pathMatch.Groups.Count > 2)
            {
                serverUrl = pathMatch.Groups[1].Value;
                socketOptions.Path = pathMatch.Groups[2].Value;
            }

            client = new SocketIOClient.SocketIO(serverUrl, socketOptions)
            {
                Serializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    Converters = [new BooleanJsonConverter()],
                })
            };
        }
        catch(Exception e)
        {
            logger.Error(e.ToStringFull());
            return false;
        }

        client.OnAny((eventName, response) =>
        {
            try
            {
                var message = response.GetValue<Message>();
                logger.Info($"Received socket message eventName {eventName}, message {Serialize(message)}");
            }
            catch (Exception)
            {
                logger.Info($"Received socket message eventName {eventName}, response {response}");
            }
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
        if (client == null)
        {
            return;
        }

        logger.Info($"Sending {Serialize(message)}");
        await client.EmitAsync("message", message);
    }

    public async Task DisconnectAsync()
    {
        if (client == null)
        {
            return;
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
        client = null;
        IsConnecting = IsConnected = false;
        ConnectedPlayersInParty = 0;
    }

    private string Serialize(object obj) => JsonConvert.SerializeObject(obj, printSettings);

    private void OnConnected(object? sender, EventArgs e)
    {
        if (client == null) { return; }

        logger.Info($"Client connected to {GetServerUrl()}");
        IsConnecting = false;
        IsConnected = true;

        if (!dalamud.PlayerState.IsLoaded)
        {
            logger.Error("PlayerState is not loaded. Disconnecting client.");
            DisconnectAsync().SafeFireAndForget();
            return;
        }
    }

    private void OnDisconnected(object? sender, string e)
    {
        logger.Info($"Client disconnected: {e}");
        Dispose();
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
