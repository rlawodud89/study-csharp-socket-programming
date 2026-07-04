using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Packet;

public static class PacketSerializer
{
    public static byte[] SerializePacket(IPacket packet)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // 임시로 길이 필드 위치 예약 (나중에 채울 것)
                writer.Write(0);

                // 패킷 ID 작성
                writer.Write((ushort)packet.Id);

                // 패킷 내용 직렬화
                packet.Serialize(writer);

                // 패킷 길이 계산 및 업데이트
                byte[] data = ms.ToArray();
                int packetLength = data.Length;

                // 길이 필드 업데이트
                using (MemoryStream updateMs = new MemoryStream(data))
                using (BinaryWriter updateWriter = new BinaryWriter(updateMs))
                {
                    updateWriter.Write(packetLength);
                }

                return data;
            }
        }
    }
}

