using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleTcpChattingClient
{
    class Program
    {
        static void Main(string[] args)
        {
            // 사용자 닉네임 입력
            Console.WriteLine("이름을 입력하세요.");
            string? clientName = Console.ReadLine();

            // 서버 IP 주소와 포트 설정
            IPAddress serverIP = IPAddress.Parse("127.0.0.1");
            int serverPort = 8080;

            // TCP 소켓 생성
            Socket clientSocket = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

            try
            {
                // 서버에 연결
                clientSocket.Connect(new IPEndPoint(serverIP, serverPort));
                Console.WriteLine($"서버 {serverIP}:{serverPort}에 연결되었습니다.");

                // 이름 데이터 송신
                byte[] nameData = Encoding.UTF8.GetBytes(clientName);
                clientSocket.Send(nameData);

                // 수신 스레드 실행
                Thread receiveThread = new Thread(() => ReceiveMessage(clientSocket));
                receiveThread.Start();

                // 메인스레드에선 입력 후 송신 실행
                while (true)
                {
                    // 사용자 입력 받기
                    Console.Write("전송할 메시지 (종료하려면 'exit' 입력): ");
                    string? message = Console.ReadLine();

                    if (string.IsNullOrEmpty(message) || message.ToLower() == "exit")
                    {
                        break;
                    }

                    // 메시지를 바이트 배열로 변환
                    byte[] sendData = Encoding.UTF8.GetBytes(message);

                    // 데이터 전송
                    clientSocket.Send(sendData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 오류 발생: {ex.Message}");
            }
            finally
            {
                // 클라이언트 소켓 닫기
                clientSocket.Close();
            }
        }

        static void ReceiveMessage(Socket clientSocket)
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[1024];

                    int received = clientSocket.Receive(buffer);

                    if (received == 0)
                    {
                        Console.WriteLine("서버와 연결이 종료되었습니다.");
                        break;
                    }

                    string message = Encoding.UTF8.GetString(buffer, 0, received);

                    Console.WriteLine("\n" + message);
                }
            }
            catch
            {
                Console.WriteLine("서버와 연결이 끊어졌습니다.");
            }
        }
    }
}