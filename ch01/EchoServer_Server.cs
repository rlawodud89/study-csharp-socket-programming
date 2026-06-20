using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleSocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            // 서버 IP 주소와 포트 설정
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            int port = 8888;

            // TCP 소켓 생성
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // IP 주소와 포트에 바인딩
                listener.Bind(new IPEndPoint(ipAddress, port));

                // 연결 대기 상태로 설정 (최대 10개 연결 대기열)
                listener.Listen(10);

                Console.WriteLine("서버가 시작되었습니다.");

                while (true)
                {
                    // 클라이언트 연결 수락
                    Console.WriteLine("클라이언트 연결 대기 중...");
                    Socket handler = listener.Accept();
                    Console.WriteLine("클라이언트와 연결되었습니다.");

                    while (true)
                    {
                        // 클라이언트로부터 데이터 수신
                        byte[] buffer = new byte[1024];
                        int received = handler.Receive(buffer);
                        string data = Encoding.UTF8.GetString(buffer, 0, received);

                        // 클라이언트가 연결 종료한 경우
                        if (received == 0) 
                        {
                            Console.WriteLine($"클라이언트와의 연결이 종료되었습니다.");
                            break;
                        }
                        
                        Console.WriteLine($"클라이언트로부터 수신: {data}");

                        // 클라이언트에게 응답 전송 (에코 서버이므로 수신받은 데이터 그대로 전송)
                        byte[] responseBuffer = Encoding.UTF8.GetBytes(data);
                        handler.Send(responseBuffer);
                    }

                    // 연결 종료
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}