using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Packet;

// 패킷 역직렬화 헬퍼 클래스
public class PacketProcessor
    {
        private readonly Dictionary<ushort, Func<IPacket>> _packetFactories = new();

        public PacketProcessor()
        {
            // 패킷 타입 등록
            RegisterPacket<ChatPacket>();
            RegisterPacket<MovePacket>();
        }

        public void RegisterPacket<T>() where T : IPacket, new()
        {
            IPacket instance = new T();
            _packetFactories[(ushort)instance.Id] = () => new T();
        }

        public IPacket DeserializePacket(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // 헤더 읽기
                int length = reader.ReadInt32();
                ushort id = reader.ReadUInt16();

                // 패킷 생성
                if (!_packetFactories.TryGetValue(id, out var factory))
                {
                    throw new InvalidOperationException($"알 수 없는 패킷 ID: {id}");
                }

                // 패킷 인스턴스 생성 및 역직렬화
                IPacket packet = factory();
                packet.Deserialize(reader);

                return packet;
            }
        }
    }
