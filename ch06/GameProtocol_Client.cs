using System;
using System.Net.Sockets;
using System.Text;
using static Program;

class Program
{
    static TcpClient _client;
    static NetworkStream _stream;
    static PacketProcessor _packetProcessor = new PacketProcessor();
    static MessageFramer _messageFramer;

    static async Task Main()
    {
        _client = new TcpClient();
        await _client.ConnectAsync("127.0.0.1", 5000);

        _stream = _client.GetStream();
        _messageFramer = new MessageFramer(_stream);

        Console.WriteLine("서버 연결 완료");

        _ = ReceiveLoop();

        while (true)
        {
            ConsoleKey key = Console.ReadKey(true).Key;
            IPacket packet = null;

            switch (key)
            {
                case ConsoleKey.D1:
                    packet = new ChatMessagePacket
                    {
                        Sender = "Player1",
                        Message = "Hello, World!"
                    };
                    break;

                case ConsoleKey.D2:
                    packet = new MovePacket
                    {
                        X = 10.0f,
                        Y = 20.0f
                    };
                    break;

                case ConsoleKey.D3:
                    packet = new ItemPickupPacket
                    {
                        ItemId = 55555
                    };
                    break;

                case ConsoleKey.D4:
                    packet = new ItemSellPacket
                    {
                        ItemId = 12345,
                        Quantity = 2
                    };
                    break;

                case ConsoleKey.D5:
                    packet = new AttackPacket
                    {
                        TargetId = 67890,
                        Damage = 50
                    };
                    break;
            }

            if (packet != null)
            {
                byte[] data = SerializePacket(packet);
                await _stream.WriteAsync(data, 0, data.Length);
            }
        }
    }

    static async Task ReceiveLoop()
    {
        byte[] buffer = new byte[1024];

        while (true)
        {
            byte[] read = await _messageFramer.ReceiveMessageAsync();

            IPacket packet = _packetProcessor.DeserializePacket(read);

            switch (packet)
            {
                case ChatMessagePacket chat:
                    Console.WriteLine($"[채팅] {chat.Sender}: {chat.Message}");
                    break;

                case MovePacket move:
                    Console.WriteLine($"[이동] {move.X}, {move.Y}");
                    break;

                case ItemPickupPacket pickup:
                    Console.WriteLine($"[아이템 습득] 아이템 ID: {pickup.ItemId}");
                    break;

                case ItemSellPacket sell:
                    Console.WriteLine($"[아이템 판매] 아이템 ID: {sell.ItemId}, 수량: {sell.Quantity}");
                    break;

                case AttackPacket attack:
                    Console.WriteLine($"[공격] 대상 ID: {attack.TargetId}, 피해량: {attack.Damage}");
                    break;

                default:
                    Console.WriteLine("알 수 없는 패킷");
                    break;
            }
        }
    }


    // 패킷 헤더 정의
    public struct PacketHeader
    {
        public int Length;   // 전체 패킷의 길이 (헤더 포함)
        public ushort Id;    // 패킷의 종류를 식별하는 ID
    }

    // 패킷 인터페이스
    public interface IPacket
    {
        ushort Id { get; }
        void Serialize(BinaryWriter writer);
        void Deserialize(BinaryReader reader);
    }

    // 채팅 메시지
    public class ChatMessagePacket : IPacket
    {
        public ushort Id => 101; // 채팅 패킷 ID

        public string Sender { get; set; } // 보낸 사람
        public string Message { get; set; } // 메시지 내용

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Sender);
            writer.Write(Message);
        }

        public void Deserialize(BinaryReader reader)
        {
            Sender = reader.ReadString();
            Message = reader.ReadString();
        }
    }

    // 플레이어 이동
    public class MovePacket : IPacket
    {
        public ushort Id => 102; // 이동 패킷 ID
        public float X { get; set; } // 이동한 좌표
        public float Y { get; set; }

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

    // 아이템 습득
    public class ItemPickupPacket : IPacket
    {
        public ushort Id => 103; // 아이템 습득 패킷 ID
        public int ItemId { get; set; } // 습득한 아이템 ID
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ItemId);
        }
        public void Deserialize(BinaryReader reader)
        {
            ItemId = reader.ReadInt32();
        }
    }

    // 아이템 판매
    public class ItemSellPacket : IPacket
    {
        public ushort Id => 104; // 아이템 판매 패킷 ID
        public int ItemId { get; set; } // 판매한 아이템 ID
        public int Quantity { get; set; } // 판매 수량
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ItemId);
            writer.Write(Quantity);
        }
        public void Deserialize(BinaryReader reader)
        {
            ItemId = reader.ReadInt32();
            Quantity = reader.ReadInt32();
        }
    }

    // 공격
    public class AttackPacket : IPacket
    {
        public ushort Id => 105; // 공격 패킷 ID
        public int TargetId { get; set; } // 공격 대상 ID
        public int Damage { get; set; } // 피해량
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(TargetId);
            writer.Write(Damage);
        }
        public void Deserialize(BinaryReader reader)
        {
            TargetId = reader.ReadInt32();
            Damage = reader.ReadInt32();
        }
    }

    // 패킷 직렬화
    static public byte[] SerializePacket(IPacket packet)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                // 임시로 길이 필드 위치 예약 (나중에 채울 것)
                writer.Write(0);

                // 패킷 ID 작성
                writer.Write(packet.Id);

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

    // 패킷 역직렬화 헬퍼 클래스
    public class PacketProcessor
    {
        private readonly Dictionary<ushort, Func<IPacket>> _packetFactories = new();

        public PacketProcessor()
        {
            // 패킷 타입 등록
            RegisterPacket<ChatMessagePacket>();
            RegisterPacket<MovePacket>();
            RegisterPacket<ItemPickupPacket>();
            RegisterPacket<ItemSellPacket>();
            RegisterPacket<AttackPacket>();
        }

        public void RegisterPacket<T>() where T : IPacket, new()
        {
            IPacket instance = new T();
            _packetFactories[instance.Id] = () => new T();
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

    public class MessageFramer
    {
        private readonly NetworkStream _stream;
        private readonly byte[] _lengthBuffer = new byte[4];
        private readonly byte[] _messageBuffer = new byte[1024 * 64]; // 64KB 최대 메시지 크기

        public MessageFramer(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task<byte[]> ReceiveMessageAsync()
        {
            // 1. Length 읽기 (전체 패킷 크기)
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                int read = await _stream.ReadAsync(_lengthBuffer, bytesRead, 4 - bytesRead);
                if (read == 0)
                    throw new EndOfStreamException("연결이 닫혔습니다.");

                bytesRead += read;
            }

            int totalLength = BitConverter.ToInt32(_lengthBuffer, 0);

            if (totalLength <= 4 || totalLength > _messageBuffer.Length)
                throw new InvalidDataException($"메시지 길이가 너무 큽니다: {totalLength}");

            // 2. 남은 데이터 길이
            int bodyLength = totalLength - 4;

            bytesRead = 0;
            while (bytesRead < bodyLength)
            {
                int read = await _stream.ReadAsync(_messageBuffer, bytesRead, bodyLength - bytesRead);
                if (read == 0)
                    throw new EndOfStreamException("연결이 닫혔습니다.");

                bytesRead += read;
            }

            // 3. 최종 패킷 조립
            byte[] packet = new byte[totalLength];

            // Length 포함 복원
            Buffer.BlockCopy(_lengthBuffer, 0, packet, 0, 4);
            Buffer.BlockCopy(_messageBuffer, 0, packet, 4, bodyLength);

            return packet;
        }
    }
}