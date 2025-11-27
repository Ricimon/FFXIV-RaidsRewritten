using JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SocketIO.Serializer.NewtonsoftJson;
using TestClient;

var client = new SocketIOClient.SocketIO("http://localhost:3000/", new()
{
    Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
})
{
    Serializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        Converters = [new BooleanJsonConverter()],
    })
};

var printSettings = new JsonSerializerSettings
{
    NullValueHandling = NullValueHandling.Ignore,
    ContractResolver = new LongNameContractResolver(),
    Converters = [new StringEnumConverter()],
};
string Serialize(object obj) => JsonConvert.SerializeObject(obj, printSettings);

client.OnAny((eventName, response) =>
{
    try
    {
        var message = response.GetValue<Message>();
        Console.WriteLine($"Received socket message eventName {eventName}, message {Serialize(message)}");
    }
    catch (Exception)
    {
        Console.WriteLine($"Received socket message eventName {eventName}, response {response}");
    }
});

client.OnConnected += async (sender, _) =>
{
    Console.WriteLine("Connected");

    var updatePlayer = new Message
    {
        action = Message.Action.UpdatePlayer,
        updatePlayer = new Message.UpdatePlayerPayload
        {
            contentId = 1001,
            name = "Test Client1",
            role = Message.UpdatePlayerPayload.Role.Tank,
            party = "test_party",
        },
    };
    Console.WriteLine($"Sending {Serialize(updatePlayer)}");
    await client.EmitAsync("message", updatePlayer);

    await Task.Delay(1500);

    var startMechanic = new Message
    {
        action = Message.Action.StartMechanic,
        startMechanic = new Message.StartMechanicPayload
        {
            requestId = "TestMechanic1",
            mechanicId = 1,
        },
    };
    for (var i = 0; i < 2; i++)
    {
        Console.WriteLine($"Sending {Serialize(startMechanic)}");
        await client.EmitAsync("message", startMechanic);
    }

    await Task.Delay(3000);

    startMechanic = new Message
    {
        action = Message.Action.StartMechanic,
        startMechanic = new Message.StartMechanicPayload
        {
            requestId = "TestMechanic2",
            mechanicId = 1,
        },
    };
    Console.WriteLine($"Sending {Serialize(startMechanic)}");
    await client.EmitAsync("message", startMechanic);
};

client.OnDisconnected += (sender, e) =>
{
    Console.WriteLine($"Disconnected: {e}");
};

client.OnError += (sender, e) =>
{
    Console.WriteLine($"Error: {e}");
};

client.OnReconnectAttempt += (sender, e) =>
{
    Console.WriteLine($"ReconnectAttempt: {e}");
};

Console.WriteLine("Connecting...");

await client.ConnectAsync();

_ = Task.Run(async () =>
{
    while (true)
    {
        if (client.Connected)
        {
            var updateStatus = new Message
            {
                action = Message.Action.UpdateStatus,
                updateStatus = new Message.UpdateStatusPayload
                {
                    worldPositionX = 1,
                    worldPositionY = 2,
                    worldPositionZ = 3,
                    isAlive = true,
                },
            };
            Console.WriteLine($"Sending {Serialize(updateStatus)}");
            _ = client.EmitAsync("message", updateStatus);
        }

        await Task.Delay(1000);
    }
});

await Task.Delay(5000);

await client.DisconnectAsync();

await Task.Delay(500);
