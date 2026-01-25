using System;
using System.Threading.Tasks;
using AsyncAwaitBestPractices;
using JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RaidsRewritten.Log;
using SocketIO.Serializer.NewtonsoftJson;

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

    public string GetServerUrl() => string.IsNullOrEmpty(configuration.ServerUrl) ? DefaultServerUrl : configuration.ServerUrl;

    public bool Connect()
    {
        if (client != null)
        {
            return false;
        }

        client = new SocketIOClient.SocketIO(GetServerUrl(), new()
        {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            ReconnectionAttempts = 3,
        })
        {
            Serializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = [new BooleanJsonConverter()],
            })
        };

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
        SendAsync(updatePlayer).SafeFireAndForget();
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
