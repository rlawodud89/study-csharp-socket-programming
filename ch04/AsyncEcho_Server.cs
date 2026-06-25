using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace AsyncEchoServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                // 서버 설정
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                int port = 8888;
                var server = new EchoServer("127.0.0.1",
                    port); // 포트

                Console.WriteLine("에코 서버 시작 중...");
                await server.StartAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 오류: {ex.Message}");
            }
        }
    }

    class EchoServer
    {
        private readonly TcpListener _listener;

        public EchoServer(string ipAddress, int port)
        {
            IPAddress iPAddress = IPAddress.Parse(ipAddress);
            _listener = new TcpListener(iPAddress, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"서버가 {_listener.LocalEndpoint}에서 시작됨");

            try
            {
                while (true)
                {
                    // 비동기적으로 클라이언트 연결 수락
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    _ = HandleClientAsync(client);
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 오류: {ex.Message}");
            }
            finally
            {
                _listener.Stop();
                Console.WriteLine("서버가 종료됨");
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            Console.WriteLine($"클라이언트 연결됨: {client.Client.RemoteEndPoint}");
            using (client)
            {
                NetworkStream stream = client.GetStream();
                byte[] buffer = new byte[1024];
                try
                {
                    while (true)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                        if (bytesRead == 0) break; // 클라이언트가 연결을 종료함

                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine($"받은 메시지: {receivedMessage}");

                        // 받은 메시지를 그대로 클라이언트에 전송
                        string responseMessage = $"[서버] {receivedMessage}";
                        byte[] responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
                        await stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"클라이언트 처리 중 오류: {ex.Message}");
                }
                finally
                {
                    Console.WriteLine($"클라이언트 연결 종료됨: {client.Client.RemoteEndPoint}");
                }
            }
        }
    }
}