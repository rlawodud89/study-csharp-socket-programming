using Microsoft.AspNetCore.SignalR;
using Study_WebServer.Packet;
using Study_WebServer.Worker;

namespace Study_WebServer.Network;

public class ChatHub : Hub
{
    private readonly SessionManager _sessionManager;
    private readonly PacketQueue _queue;

    public ChatHub(SessionManager manager,
                   PacketQueue queue)
    {
        _sessionManager = manager;
        _queue = queue;
    }

    public override Task OnConnectedAsync()
    {
        _sessionManager.Add(
            new ClientSession(Context.ConnectionId));

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? ex)
    {
        _sessionManager.Remove(Context.ConnectionId);

        return base.OnDisconnectedAsync(ex);
    }

    public Task Send(ChatPacket packet)
    {
        var session =
            _sessionManager.Find(Context.ConnectionId);

        if (session != null)
            _queue.Push(session, packet);

        return Task.CompletedTask;
    }
}
