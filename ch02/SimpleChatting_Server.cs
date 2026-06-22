using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleTcpChattingServer
{
    class Program
    {
        static Dictionary<Socket, string> clients = new();

        static void Main(string[] args)
        {
            // 서버 IP 주소와 포트 설정
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            int port = 8080;

            // TCP 소켓 생성
            Socket serverSocket = new Socket(AddressFamily.InterNetwork,
                                            SocketType.Stream,
                                            ProtocolType.Tcp);

            try
            {
                // 소켓과 로컬 엔드포인트 연결
                serverSocket.Bind(new IPEndPoint(ipAddress, port));

                // 연결 대기열 설정
                serverSocket.Listen(10);

                Console.WriteLine("채팅 서버가 시작되었습니다.");
                Console.WriteLine($"IP 주소: {ipAddress}, 포트: {port}");

                while (true)
                {
                    // 클라이언트 연결 수락
                    Socket clientSocket = serverSocket.Accept();

                    // 데이터 수신 버퍼
                    byte[] buffer = new byte[1024];

                    // 이름 데이터 수신
                    int nameRead = clientSocket.Receive(buffer);
                    string nameData = Encoding.UTF8.GetString(buffer, 0, nameRead);
                    BroadCast(clientSocket, $"{nameData}님이 입장하셨습니다.");
                    clients.Add(clientSocket, nameData);

                    // 클라이언트 스레드 실행
                    Thread clientThread = new Thread(() => ClientThread(clientSocket));
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 오류 발생: {ex.Message}");
            }
            finally
            {
                // 서버 소켓 닫기
                serverSocket.Close();
            }
        }

        static void ClientThread(Socket clientSocket)
        {
            // 각 클라이언트에게서 데이터 수신 받음
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[1024];

                    int received = clientSocket.Receive(buffer);

                    if (received == 0)
                    {
                        string exitMessage = $"{clients[clientSocket]}님이 퇴장하셨습니다.";
                        BroadCast(clientSocket, exitMessage);
                        clients.Remove(clientSocket);
                        break;
                    }

                    // 다른 클라이언트들에게 데이터 수신
                    string message = $"{clients[clientSocket]}: " + Encoding.UTF8.GetString(buffer, 0, received);
                    BroadCast(clientSocket, message);
                }
            }
            catch
            {
                string exitMessage = $"{clients[clientSocket]}님이 퇴장하셨습니다.";
                BroadCast(clientSocket, exitMessage);
                clients.Remove(clientSocket);
            }
        }

        static void BroadCast(Socket senderSocket, string message)
        {
            Console.WriteLine(message);

            foreach (var (socket, name) in clients)
            {
                if (senderSocket == socket) continue;

                byte[] sendData = Encoding.UTF8.GetBytes(message);
                socket.Send(sendData);
            }
        }
    }
}