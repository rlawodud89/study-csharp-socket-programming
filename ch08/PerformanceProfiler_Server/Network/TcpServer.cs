using System.Net;
using System.Net.Sockets;
using Study_Server.Packet;
using Study_Server.Profiler;

namespace Study_Server.Network;

public class TcpServer
{
    private readonly TcpListener _listener;
    private readonly SessionManager _sessionManager;
    private readonly PacketProcessor _packetProcessor;
    private readonly PerformanceProfiler _profiler;

    private int _sessionIdGenerator = 0;

    private bool _isRunning = false;

    public TcpServer(int port,
                     SessionManager sessionManager,
                     PacketProcessor packetProcessor)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _sessionManager = sessionManager;
        _packetProcessor = packetProcessor;
        _profiler = new PerformanceProfiler();
    }

    public async Task StartAsync()
    {
        _listener.Start();
        _isRunning = true;

        Console.WriteLine($"Server Started : {_listener.LocalEndpoint}");

        try
        {
            while (_isRunning)
            {
                TcpClient client =
                    await _listener.AcceptTcpClientAsync();

                int sessionId = GenerateSessionId();

                TcpSession session =
                    new TcpSession(
                        client,
                        sessionId,
                        _packetProcessor,
                        _sessionManager,
                        _profiler
                    );

                _ = session.StartAsync();

                Console.WriteLine($"Client Connected : {sessionId}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server Error : {ex.Message}");
        }
    }

    private int GenerateSessionId()
    {
        return Interlocked.Increment(ref _sessionIdGenerator);
    }

    public void Stop()
    {
        _isRunning = false;

        try
        {
            _listener.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server Stop Error : {ex.Message}");
        }
    }
}