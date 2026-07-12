
namespace Study_WebServer.Network;

public class ClientSession
{
    public string ConnectionId { get; }

    public ClientSession(string id)
    {
        ConnectionId = id;
    }
}
