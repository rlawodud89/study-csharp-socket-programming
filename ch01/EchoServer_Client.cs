using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleSocketClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // 서버 IP 주소와 포트 설정
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            int port = 8888;

            try
            {
                // TCP 소켓 생성
                Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                // 서버에 연결
                client.Connect(new IPEndPoint(ipAddress, port));

                Console.WriteLine("서버에 연결되었습니다.");

                while (true)
                {
                    // 전송 데이터 입력
                    string message;
                    message = Console.ReadLine();

                    if (message == "Q") break; // Q 입력하는 경우 전송 종료

                    // 서버에 데이터 전송
                    byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
                    client.Send(messageBuffer);

                    // 서버로부터 응답 수신
                    byte[] buffer = new byte[1024];
                    int received = client.Receive(buffer);
                    string response = Encoding.UTF8.GetString(buffer, 0, received);

                    Console.WriteLine($"서버로부터 수신: {response}");
                }

                // 연결 종료
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}