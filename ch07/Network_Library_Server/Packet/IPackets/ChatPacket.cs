using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Packet;

public class ChatPacket : IPacket
{
    public PacketId Id => PacketId.Chat;

    public string Message { get; set; } = string.Empty;
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Message);
    }

    public void Deserialize(BinaryReader reader)
    {
        Message = reader.ReadString();
    }
}
