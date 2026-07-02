using System.Net;
using System.Net.Sockets;

class Program
{
    static void Main(string[] args)
    {
        int maxConnections = 10000; // 최대 연결 수
        int receiveBufferSize = 1024; // 수신 버퍼 크기
        HighPerformanceEchoServer server = new HighPerformanceEchoServer(maxConnections, receiveBufferSize);
        server.Initialize();
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 5000);
        server.Start(localEndPoint);
        Console.WriteLine("서버가 시작되었습니다. 종료하려면 Enter 키를 누르세요.");
        Console.ReadLine();
        server.Stop();
    }
}


public class HighPerformanceEchoServer
{
    private readonly int _maxConnections;
    private readonly int _receiveBufferSize;
    private readonly BufferManager _bufferManager;
    private readonly SocketAsyncEventArgsPool _readPool;
    private readonly SocketAsyncEventArgsPool _writePool;
    private readonly Semaphore _maxConnectionsEnforcer;
    private Socket _listenSocket;
    private long _connectedClients;
    private long _requestCount;
    private readonly CancellationTokenSource _cts;

    public HighPerformanceEchoServer(int maxConnections, int receiveBufferSize)
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

        _connectedClients = 0;
        _requestCount = 0;
        _cts = new CancellationTokenSource();
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

        _ = MonitorAsync(_cts.Token); // 모니터링 시작 (비동기)
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
                Interlocked.Increment(ref _connectedClients);

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

        // 데이터 처리 로직 (에코 서버이므로 바로 동일한 데이터 송신)
        SendResponse(token.Socket, data);

        Interlocked.Increment(ref _requestCount);

        // 다시 수신 대기
        bool willRaiseEvent = token.Socket.ReceiveAsync(e);
        if (!willRaiseEvent)
        {
            ProcessReceive(e);
        }
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

        Interlocked.Decrement(ref _connectedClients);
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

    private async Task MonitorAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(1000, token);

                long rps = Interlocked.Exchange(ref _requestCount, 0); // _requestCount 값 읽고 0으로 초기화

                Console.WriteLine($"RPS : {rps}");
                Console.WriteLine($"Connections : {_connectedClients}");
            }
        }
        catch (OperationCanceledException)
        {
            // 취소 요청 시 정상 종료
        }
    }


    // 연결당 상태 정보를 저장하는 클래스
    private class AsyncUserToken
    {
        public Socket Socket { get; set; }
        // 추가 상태 정보 저장 가능
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

}