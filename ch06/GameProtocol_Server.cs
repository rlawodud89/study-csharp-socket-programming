using System;
using System.Net;
using System.Net.Sockets;

class Program
{
    static void Main(string[] args)
    {
        int maxConnections = 10000; // 최대 연결 수
        int receiveBufferSize = 1024; // 수신 버퍼 크기
        PacketEchoServer server = new PacketEchoServer(maxConnections, receiveBufferSize);
        server.Initialize();
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 5000);
        server.Start(localEndPoint);
        Console.WriteLine("서버가 시작되었습니다. 종료하려면 Enter 키를 누르세요.");
        Console.ReadLine();
        server.Stop();
    }
}




public class PacketEchoServer
{
    private readonly int _maxConnections;
    private readonly int _receiveBufferSize;
    private readonly BufferManager _bufferManager;
    private readonly SocketAsyncEventArgsPool _readPool;
    private readonly SocketAsyncEventArgsPool _writePool;
    private readonly Semaphore _maxConnectionsEnforcer;
    private Socket _listenSocket;
    private readonly CancellationTokenSource _cts;

    private readonly PacketProcessor _packetProcessor;

    public PacketEchoServer(int maxConnections, int receiveBufferSize)
    {
        _maxConnections = maxConnections;
        _receiveBufferSize = receiveBufferSize;

        // 모든 소켓 작업에 사용할 버퍼 풀 생성
        // 수신 버퍼와 송신 버퍼를 각각 따로 할당
        int totalBytes = maxConnections * 2 * receiveBufferSize;
        _bufferManager = new BufferManager(totalBytes, receiveBufferSize);

        _readPool = new SocketAsyncEventArgsPool(maxConnections);
        _writePool = new SocketAsyncEventArgsPool(maxConnections);

        // 최대 연결 수 제한을 위한 세마포어
        _maxConnectionsEnforcer = new Semaphore(maxConnections, maxConnections);

        _cts = new CancellationTokenSource();

        _packetProcessor = new PacketProcessor();
    }

    public void Initialize()
    {
        // 버퍼 풀 초기화
        _bufferManager.InitializeBuffer();

        // SocketAsyncEventArgs 객체 풀 준비
        SocketAsyncEventArgs readArgs;

        for (int i = 0; i < _maxConnections; i++)
        {
            // 수신용 SocketAsyncEventArgs 준비
            readArgs = new SocketAsyncEventArgs();
            readArgs.Completed += IO_Completed;
            readArgs.UserToken = new AsyncUserToken();

            // 버퍼 할당
            _bufferManager.SetBuffer(readArgs);

            // 풀에 추가
            _readPool.Push(readArgs);
        }

        // 송신용 SocketAsyncEventArgs 준비 (비슷한 방식)
        for (int i = 0; i < _maxConnections; i++)
        {
            SocketAsyncEventArgs writeArgs = new SocketAsyncEventArgs();
            writeArgs.Completed += IO_Completed;
            writeArgs.UserToken = new AsyncUserToken();

            _bufferManager.SetBuffer(writeArgs);
            _writePool.Push(writeArgs);
        }
    }

    public void Start(IPEndPoint localEndPoint)
    {
        // 서버 소켓 생성 및 바인딩
        _listenSocket = new Socket(localEndPoint.AddressFamily,
                                  SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(localEndPoint);

        // 최대 10,000개의 대기 연결 허용
        _listenSocket.Listen(10000);

        // 연결 수락 시작
        StartAccept(null);

        Console.WriteLine($"서버가 {localEndPoint}에서 시작됨");
    }

    private void StartAccept(SocketAsyncEventArgs acceptEventArg)
    {
        if (acceptEventArg == null)
        {
            acceptEventArg = new SocketAsyncEventArgs();
            acceptEventArg.Completed += AcceptEventArg_Completed;
        }
        else
        {
            // 소켓 핸들 정리
            acceptEventArg.AcceptSocket = null;
        }

        // 새 연결을 받기 전에 세마포어 대기
        _maxConnectionsEnforcer.WaitOne();

        bool willRaiseEvent = _listenSocket.AcceptAsync(acceptEventArg);
        if (!willRaiseEvent)
        {
            // 동기적으로 완료된 경우
            ProcessAccept(acceptEventArg);
        }
    }

    private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            // 풀에서 수신용 SocketAsyncEventArgs 가져오기
            SocketAsyncEventArgs readEventArgs = _readPool.Pop();

            // 풀이 비어있으면 연결 거부
            if (readEventArgs == null)
            {
                Console.WriteLine("서버가 최대 용량에 도달했습니다. 연결 거부됨.");
                e.AcceptSocket.Close();
            }
            else
            {
                // 새 소켓에 대한 참조 저장
                AsyncUserToken token = (AsyncUserToken)readEventArgs.UserToken;
                token.Socket = e.AcceptSocket;

                Console.WriteLine($"클라이언트가 연결됨: {e.AcceptSocket.RemoteEndPoint}");

                // 데이터 수신 시작
                bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(readEventArgs);
                if (!willRaiseEvent)
                {
                    ProcessReceive(readEventArgs);
                }
            }

            // 다음 연결 수락 준비
            StartAccept(e);
        }
        else
        {
            // 오류 발생 시 다시 시도
            StartAccept(e);
        }
    }

    private void IO_Completed(object sender, SocketAsyncEventArgs e)
    {
        // 완료된 I/O 작업의 유형에 따라 처리
        switch (e.LastOperation)
        {
            case SocketAsyncOperation.Receive:
                ProcessReceive(e);
                break;
            case SocketAsyncOperation.Send:
                ProcessSend(e);
                break;
            default:
                throw new ArgumentException("지원되지 않는 작업 유형");
        }
    }

    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        AsyncUserToken token = (AsyncUserToken)e.UserToken;

        // 연결이 정상적으로 닫혔거나 오류가 발생한 경우
        if (e.BytesTransferred == 0 || e.SocketError != SocketError.Success)
        {
            CloseClientSocket(e);
            return;
        }

        // 수신된 데이터 처리
        byte[] data = new byte[e.BytesTransferred];
        Buffer.BlockCopy(e.Buffer, e.Offset, data, 0, e.BytesTransferred);

        // 패킷 프레이머를 사용하여 완전한 패킷 추출
        List<byte[]> completePackets = token.Framer.Push(data);

        foreach (var packetData in completePackets)
        {
            try
            {
                IPacket packet = _packetProcessor.DeserializePacket(packetData);

                // 패킷 종류에 따른 처리
                HandlePacket(token.Socket, packet);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"패킷 처리 중 오류: {ex.Message}");
            }
        }

        // 다시 수신 대기
        bool willRaiseEvent = token.Socket.ReceiveAsync(e);
        if (!willRaiseEvent)
        {
            ProcessReceive(e);
        }
    }

    private void HandlePacket(Socket socket, IPacket packet)
    {
        // 패킷 종류에 따라 콘솔에 출력
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

        // 받은 패킷을 직렬화해서 그대로 클라이언트에게 에코
        byte[] responseData = SerializePacket(packet);
        SendResponse(socket, responseData);
    }

    private void SendResponse(Socket socket, byte[] data)
    {
        // 풀에서 송신용 SocketAsyncEventArgs 가져오기
        SocketAsyncEventArgs writeEventArgs = _writePool.Pop();

        if (writeEventArgs == null)
        {
            // 풀이 고갈되었으면 다시 시도를 위해 큐에 넣을 수 있음
            // 여기서는 간단히 오류 로그만 남김
            Console.WriteLine("송신 풀이 고갈되었습니다.");
            return;
        }

        // 버퍼에 데이터 복사
        Buffer.BlockCopy(data, 0, writeEventArgs.Buffer, writeEventArgs.Offset, data.Length);
        writeEventArgs.SetBuffer(writeEventArgs.Offset, data.Length);

        AsyncUserToken token = (AsyncUserToken)writeEventArgs.UserToken;
        token.Socket = socket;

        // 비동기 전송 시작
        bool willRaiseEvent = socket.SendAsync(writeEventArgs);
        if (!willRaiseEvent)
        {
            ProcessSend(writeEventArgs);
        }
    }

    private void ProcessSend(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            // 전송 완료, SocketAsyncEventArgs 재사용을 위해 풀에 반환
            AsyncUserToken token = (AsyncUserToken)e.UserToken;
            token.Socket = null;
            _writePool.Push(e);
        }
        else
        {
            CloseClientSocket(e);
        }
    }

    private void CloseClientSocket(SocketAsyncEventArgs e)
    {
        AsyncUserToken token = e.UserToken as AsyncUserToken;

        // 소켓 연결 종료
        try
        {
            token.Socket.Shutdown(SocketShutdown.Both);
        }
        catch (Exception) { /* 이미 닫혀 있을 수 있음 */ }

        token.Socket.Close();
        token.Socket = null;

        // 연결 개수 제한 세마포어 증가
        _maxConnectionsEnforcer.Release();

        // SocketAsyncEventArgs를 풀로 반환
        _readPool.Push(e);
    }

    // 서버 중지
    public void Stop()
    {
        _cts.Cancel();

        try
        {
            _listenSocket.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"서버 종료 중 오류: {ex.Message}");
        }
    }


    // 연결당 상태 정보를 저장하는 클래스
    class AsyncUserToken
    {
        public Socket Socket { get; set; }

        public MessageFramer Framer { get; } = new MessageFramer();
    }


    // 버퍼 관리를 위한 클래스
    private class BufferManager
    {
        private readonly int _bufferSize;
        private readonly int _totalBytes;
        private byte[] _buffer;
        private Stack<int> _freeIndexPool;
        private int _currentIndex;
        private readonly object _lockObject = new object();

        public BufferManager(int totalBytes, int bufferSize)
        {
            _totalBytes = totalBytes;
            _bufferSize = bufferSize;
        }

        public void InitializeBuffer()
        {
            _buffer = new byte[_totalBytes];
            _freeIndexPool = new Stack<int>();
            _currentIndex = 0;
        }

        public bool SetBuffer(SocketAsyncEventArgs args)
        {
            lock (_lockObject)
            {
                if (_freeIndexPool.Count > 0)
                {
                    // 이전에 사용했던 인덱스 재사용
                    int offset = _freeIndexPool.Pop();
                    args.SetBuffer(_buffer, offset, _bufferSize);
                }
                else
                {
                    // 새 인덱스 할당
                    if ((_totalBytes - _bufferSize) < _currentIndex)
                    {
                        return false; // 버퍼 풀 고갈
                    }

                    args.SetBuffer(_buffer, _currentIndex, _bufferSize);
                    _currentIndex += _bufferSize;
                }

                return true;
            }
        }

        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            lock (_lockObject)
            {
                _freeIndexPool.Push(args.Offset);
                args.SetBuffer(null, 0, 0);
            }
        }
    }


    private class SocketAsyncEventArgsPool
    {
        private readonly Stack<SocketAsyncEventArgs> _pool;

        public SocketAsyncEventArgsPool(int capacity)
        {
            _pool = new Stack<SocketAsyncEventArgs>(capacity);
        }

        public void Push(SocketAsyncEventArgs item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (_pool)
            {
                _pool.Push(item);
            }
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    return _pool.Pop();
                }
                else
                {
                    return null;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_pool)
                {
                    return _pool.Count;
                }
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
    public byte[] SerializePacket(IPacket packet)
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
        private readonly List<byte> _buffer = new List<byte>();

        public List<byte[]> Push(byte[] data)
        {
            // 1. 들어온 데이터 누적
            _buffer.AddRange(data);

            List<byte[]> result = new List<byte[]>();

            while (true)
            {
                // 2. 최소 헤더 크기 체크 (Length = 4 bytes)
                if (_buffer.Count < 4)
                    break;

                // 3. Length 읽기 (패킷 전체 길이)
                int length = BitConverter.ToInt32(_buffer.ToArray(), 0);

                // 잘못된 데이터 방어
                if (length <= 0)
                {
                    _buffer.Clear();
                    break;
                }

                // 4. 아직 패킷이 다 안 왔으면 대기
                if (_buffer.Count < length)
                    break;

                // 5. 완성된 패킷 추출
                byte[] packet = _buffer.GetRange(0, length).ToArray();
                result.Add(packet);

                // 6. 사용한 데이터 제거
                _buffer.RemoveRange(0, length);
            }

            return result;
        }
    }

}
