using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Packet;

public interface IPacket
{
    public PacketId Id { get; }

    public void Serialize(BinaryWriter writer);

    public void Deserialize(BinaryReader reader);
}


