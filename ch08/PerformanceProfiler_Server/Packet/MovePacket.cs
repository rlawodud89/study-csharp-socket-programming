using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Packet;

public class MovePacket : IPacket
{
    public PacketId Id => PacketId.Move;

    public float X { get; set; } = 0.0f;
    public float Y { get; set; } = 0.0f;

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(X);
        writer.Write(Y);
    }

    public void Deserialize(BinaryReader reader)
    {
        X = reader.ReadSingle();
        Y = reader.ReadSingle();
    }
}

