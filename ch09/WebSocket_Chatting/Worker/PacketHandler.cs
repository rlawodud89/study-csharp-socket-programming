using Microsoft.AspNetCore.SignalR;
using Study_WebServer.Network;
using Study_WebServer.Packet;

namespace Study_WebServer.Worker;

public class PacketHandler
{
    private readonly IHubContext<ChatHub> _hub;

    public PacketHandler(IHubContext<ChatHub> hub)
    {
        _hub = hub;
    }

    public async void Handle(ClientSession session,
                             ChatPacket packet)
    {
        Console.WriteLine(
            $"{packet.Sender} : {packet.Message}");

        await _hub.Clients.All.SendAsync(
            "ReceiveMessage",
            packet.Sender,
            packet.Message);
    }
}
