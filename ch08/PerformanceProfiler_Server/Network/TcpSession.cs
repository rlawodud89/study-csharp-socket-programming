using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Study_Server.Packet;
using Study_Server.Profiler;

namespace Study_Server.Network;


public class TcpSession
{
    public int SessionId { get; set; }

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    private readonly SessionManager _sessionManager;
    private readonly MessageFramer _messageFramer;
    private readonly PacketProcessor _packetProcessor;
    private readonly PerformanceProfiler _profiler;

    public TcpSession(TcpClient client, int sessionId, PacketProcessor packetProcessor, SessionManager sessionManager, PerformanceProfiler profiler)
    {
        _client = client;
        _stream = client.GetStream();
        SessionId = sessionId;
        _sessionManager = sessionManager;
        _messageFramer = new MessageFramer(_stream);
        _packetProcessor = packetProcessor;
        _profiler = profiler;


        // 세션을 세션 매니저에 등록
        sessionManager.AddSession(sessionId, this);
    }

    public async Task StartAsync()
    {
        Console.WriteLine($"PlayerSession 시작 : {_client.Client.RemoteEndPoint}");

        await ReceiveLoopAsync();
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (true)
            {
                byte[] read = await _profiler.MeasureAsync("Receive",
                    () => _messageFramer.ReceiveMessageAsync()
                );

                IPacket packet = _profiler.Measure("Deserialize",
                    () => _packetProcessor.DeserializePacket(read)
                );

                await _profiler.MeasureAsync("Handle",
                   () => PacketHandler.HandlePacket(this, packet)
                );
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task SendAsync(byte[] data)
    {
        await _profiler.MeasureAsync("Send", async () =>
        {
            await _stream.WriteAsync(data);
        });
    }

    private void Disconnect()
    {
        _sessionManager.Remove(this);

        _stream.Close();
        _client.Close();
    }
}
