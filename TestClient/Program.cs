var client = new SocketIOClient.SocketIO("http://localhost:3000/", new()
{
    Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
});

client.OnAny((eventName, response) =>
{
    Console.WriteLine($"Received socket message eventName {eventName}, response {response}");
});

client.OnConnected += async (sender, _) =>
{
    Console.WriteLine("Connected");
    await client.EmitAsync("message", "Hello, socket");
    await client.EmitAsync("message");
    var tuple = new Tuple<int, string>(2, "Tuple");
    await client.EmitAsync("message", tuple);
    await client.EmitAsync("message-with-ack", (response) => { Console.WriteLine($"Server ack {response}"); }, "Ack!");
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

await Task.Delay(5000);

await client.DisconnectAsync();

await Task.Delay(1000);
