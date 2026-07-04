using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Study_Server.Packet;

namespace Study_Server.Network;


public class TcpSession
{
    public int SessionId { get; set; }

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;

    private readonly SessionManager _sessionManager;
    private readonly MessageFramer _messageFramer;
    private readonly PacketProcessor _packetProcessor;

    public TcpSession(TcpClient client, int sessionId, PacketProcessor packetProcessor, SessionManager sessionManager)
    {
        _client = client;
        _stream = client.GetStream();
        SessionId = sessionId;
        _sessionManager = sessionManager;
        _messageFramer = new MessageFramer(_stream);
        _packetProcessor = packetProcessor;

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
                byte[] read = await _messageFramer.ReceiveMessageAsync();

                IPacket packet = _packetProcessor.DeserializePacket(read);

                // 핸들러 호출 (현재는 생략)
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
        await _stream.WriteAsync(data);
    }

    private void Disconnect()
    {
        _sessionManager.Remove(this);

        _stream.Close();
        _client.Close();
    }
}
