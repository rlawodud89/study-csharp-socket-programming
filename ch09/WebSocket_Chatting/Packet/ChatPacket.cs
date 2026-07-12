
namespace Study_WebServer.Packet;

public class ChatPacket
{
    public PacketType Type { get; set; }

    public string Sender { get; set; }

    public string Message { get; set; }
}