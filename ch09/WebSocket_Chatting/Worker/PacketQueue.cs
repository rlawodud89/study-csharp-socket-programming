using Study_WebServer.Network;
using Study_WebServer.Packet;
using System.Collections.Concurrent;

namespace Study_WebServer.Worker;

public class PacketQueue
{
    private readonly BlockingCollection<(ClientSession, ChatPacket)> _queue
        = new();

    public void Push(ClientSession session, ChatPacket packet)
    {
        _queue.Add((session, packet));
    }

    public (ClientSession, ChatPacket) Pop()
    {
        return _queue.Take();
    }
}
