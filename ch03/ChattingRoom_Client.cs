using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatClient
{
    class Program
    {
        private static Socket? clientSocket;
        private static bool isConnected = false;

        static void Main(string[] args)
        {
            Console.WriteLine("채팅 클라이언트 시작");

            try
            {
                // 서버 정보 설정
                IPAddress serverIP = IPAddress.Parse("127.0.0.1");
                int serverPort = 8888;

                // 소켓 생성
                clientSocket = new Socket(AddressFamily.InterNetwork,
                                        SocketType.Stream,
                                        ProtocolType.Tcp);

                // 서버 연결
                Console.WriteLine($"서버 {serverIP}:{serverPort}에 연결 중...");
                clientSocket.Connect(new IPEndPoint(serverIP, serverPort));

                // 연결 성공
                isConnected = true;
                Console.WriteLine("서버에 연결되었습니다.");

                // 메시지 수신 스레드 시작
                Thread receiveThread = new(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();

                // 메시지 입력 및 전송
                while (isConnected)
                {
                    string? message = Console.ReadLine();

                    if (string.IsNullOrEmpty(message))
                        continue;

                    if (message.ToLower() == "/exit")
                        break;

                    // 메시지 전송
                    SendMessage(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}");
            }
            finally
            {
                // 연결 종료
                CloseConnection();
            }

            Console.WriteLine("프로그램을 종료하려면 아무 키나 누르세요...");
            Console.ReadKey();
        }

        // 메시지 수신 메서드
        static void ReceiveMessages()
        {
            try
            {
                byte[] buffer = new byte[1024];

                while (isConnected && clientSocket != null)
                {
                    try
                    {
                        // 메시지 수신
                        int bytesRead = clientSocket.Receive(buffer);

                        if (bytesRead == 0)
                        {
                            // 서버 연결 종료
                            Console.WriteLine("서버와의 연결이 종료되었습니다.");
                            isConnected = false;
                            break;
                        }

                        // 수신된 메시지 처리
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Console.WriteLine(message);
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"메시지 수신 오류: {ex.Message}");
                        isConnected = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"수신 스레드 오류: {ex.Message}");
            }
            finally
            {
                // 연결 종료
                isConnected = false;
                CloseConnection();
            }
        }

        // 메시지 전송 메서드
        static void SendMessage(string message)
        {
            if (isConnected && clientSocket != null)
            {
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    clientSocket.Send(data);
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"메시지 전송 오류: {ex.Message}");
                    isConnected = false;
                }
            }
        }

        // 연결 종료 메서드
        static void CloseConnection()
        {
            if (clientSocket != null)
            {
                try
                {
                    if (clientSocket.Connected)
                    {
                        clientSocket.Shutdown(SocketShutdown.Both);
                    }
                }
                catch (SocketException)
                {
                    // 이미 연결이 끊겼을 경우 무시
                }
                finally
                {
                    clientSocket.Close();
                    clientSocket = null;
                }

                Console.WriteLine("서버와의 연결이 종료되었습니다.");
            }

            isConnected = false;
        }
    }
}