using JsonConverters;
using SocketIO.Serializer.NewtonsoftJson;
using TestClient;

var client = new SocketIOClient.SocketIO("http://localhost:3000/", new()
{
    Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
})
{
    Serializer = new NewtonsoftJsonSerializer(new Newtonsoft.Json.JsonSerializerSettings
    {
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
        Converters = [new BooleanJsonConverter()],
    })
};

client.OnAny((eventName, response) =>
{
    Console.WriteLine($"Received socket message eventName {eventName}, response {response}");
});

client.OnConnected += async (sender, _) =>
{
    Console.WriteLine("Connected");

    // Testing messages
    //await client.EmitAsync("message", "Hello, socket");
    //await client.EmitAsync("message");
    //var tuple = new Tuple<int, string>(2, "Tuple");
    //await client.EmitAsync("message", tuple);
    //await client.EmitAsync("message-with-ack", (response) => { Console.WriteLine($"Server ack {response}"); }, "Ack!");

    await client.EmitAsync("message", new Message
    {
        action = Message.Action.UpdatePlayer,
        updatePlayer = new Message.UpdatePlayerPayload
        {
            id = 1,
            name = "Test Client1",
            role = Message.UpdatePlayerPayload.Role.Tank,
            party = "test_party",
        },
    });
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

//_ = Task.Run(async () =>
//{
//    while (true)
//    {
//        if (client.Connected)
//        {
//            _ = client.EmitAsync("message", new Message
//            {
//                action = Message.Action.UpdateStatus,
//                updateStatus = new Message.UpdateStatusPayload
//                {
//                    worldPositionX = 1,
//                    worldPositionY = 2,
//                    worldPositionZ = 3,
//                    isAlive = true,
//                },
//            });
//        }

//        await Task.Delay(1000);
//    }
//});

await Task.Delay(5000);

await client.DisconnectAsync();

await Task.Delay(500);
