using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    class Program
    {
        // 연결된 클라이언트 목록
        public static readonly List<ClientHandler> clients = new();
        public static readonly object clientsLock = new();
        public static readonly Dictionary<string, ChatRoom> rooms = new();
        public static readonly object roomsLock = new();

        static void Main(string[] args)
        {
            // 서버 소켓 생성
            Socket serverSocket = new(AddressFamily.InterNetwork,
                                    SocketType.Stream,
                                    ProtocolType.Tcp);

            try
            {
                // 서버 설정
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                int port = 8888;
                serverSocket.Bind(new IPEndPoint(ipAddress, port));
                serverSocket.Listen(10);

                Console.WriteLine($"채팅 서버가 시작되었습니다. ({ipAddress}:{port})");

                while (true)
                {
                    Console.WriteLine("클라이언트 연결 대기 중...");

                    // 클라이언트 연결 수락
                    Socket clientSocket = serverSocket.Accept();

                    // 클라이언트 처리기 생성
                    ClientHandler clientHandler = new(clientSocket);

                    // 연결된 클라이언트 추가
                    lock (clientsLock)
                    {
                        clients.Add(clientHandler);
                    }

                    // 클라이언트 처리 스레드 시작
                    Thread clientThread = new(clientHandler.HandleClient);
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 오류: {ex.Message}");
            }
            finally
            {
                // 서버 소켓 닫기
                serverSocket.Close();
            }
        }

        // 모든 클라이언트에게 메시지 브로드캐스팅
        public static void BroadcastMessage(string message, ClientHandler? sender = null)
        {
            lock (clientsLock)
            {
                foreach (ClientHandler client in clients)
                {
                    // 송신자에게는 메시지 전송 안 함 (옵션)
                    if (client != sender)
                    {
                        try
                        {
                            client.SendMessage(message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"메시지 전송 오류: {ex.Message}");
                        }
                    }
                }
            }
        }

        public static void BroadcastMessageToRoom(string message, ChatRoom room, ClientHandler? sender = null)
        {
            foreach (ClientHandler client in room.clients)
            {
                // 송신자에게는 메시지 전송 안 함 (옵션)
                if (client != sender)
                {
                    try
                    {
                        client.SendMessage(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"메시지 전송 오류: {ex.Message}");
                    }
                }
            }
        }

        // 연결 종료된 클라이언트 제거
        public static void RemoveClient(ClientHandler client)
        {
            lock (clientsLock)
            {
                if (clients.Contains(client))
                {
                    clients.Remove(client);
                    Console.WriteLine($"클라이언트가 제거되었습니다. 현재 연결 수: {clients.Count}");
                }
            }
        }
    }

    class ChatRoom
    {
        public string roomName = "";
        public List<ClientHandler> clients = new();
    }

    // 클라이언트 처리 클래스
    class ClientHandler
    {
        private readonly Socket clientSocket;
        private string nickname = "Guest";
        private bool isConnected = true;
        private ChatRoom? currentRoom;

        public ClientHandler(Socket socket)
        {
            clientSocket = socket;

            // 클라이언트 정보 출력
            IPEndPoint? remoteEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"클라이언트 연결됨: {remoteEndPoint?.Address}:{remoteEndPoint?.Port}");
        }

        // 클라이언트 처리 메서드
        public void HandleClient()
        {
            try
            {
                // 환영 메시지 전송
                SendMessage("채팅 서버에 연결되었습니다. 닉네임을 입력하세요: /nick <닉네임>");

                // 클라이언트로부터 데이터 수신 대기
                byte[] buffer = new byte[1024];

                while (isConnected)
                {
                    try
                    {
                        int bytesRead = clientSocket.Receive(buffer);

                        if (bytesRead == 0)
                        {
                            // 클라이언트 연결 종료
                            break;
                        }

                        // 수신된 메시지 처리
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        ProcessMessage(message);
                    }
                    catch (SocketException)
                    {
                        // 소켓 오류 시 연결 종료
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 처리 오류: {ex.Message}");
            }
            finally
            {
                // 연결 종료 처리
                isConnected = false;
                CloseConnection();

                // 퇴장 메시지 브로드캐스팅
                Program.BroadcastMessage($"[시스템] {nickname}님이 퇴장했습니다.");

                // 클라이언트 목록에서 제거
                Program.RemoveClient(this);
            }
        }

        // 메시지 처리 메서드
        private void ProcessMessage(string message)
        {
            // 닉네임 변경 명령 처리
            if (message.StartsWith("/nick "))
            {
                string newNickname = message.Substring(6).Trim();

                if (!string.IsNullOrEmpty(newNickname))
                {
                    string oldNickname = nickname;
                    nickname = newNickname;

                    // 변경 사실 알림
                    SendMessage($"닉네임이 {newNickname}(으)로 변경되었습니다.");

                    // 다른 사용자에게 알림
                    if (currentRoom != null)
                        Program.BroadcastMessageToRoom($"[시스템] {oldNickname}님이 {newNickname}(으)로 닉네임을 변경했습니다.", currentRoom, this);
                }
                else
                {
                    SendMessage("올바르지 않은 닉네임입니다.");
                }
            }
            // 채팅방 입장 명령 처리
            else if (message.StartsWith("/join "))
            {
                string roomName = message.Substring(6).Trim();
                if (!string.IsNullOrEmpty(roomName))
                {
                    lock (Program.roomsLock)
                    {
                        if (!Program.rooms.ContainsKey(roomName))
                        {
                            SendMessage("채팅방이 존재하지 않습니다.");
                        }
                        // 현재 채팅방에서 나가기
                        if (currentRoom != null)
                        {
                            currentRoom.clients.Remove(this);
                            Program.BroadcastMessageToRoom($"[시스템] {nickname}님이 퇴장했습니다.", currentRoom, this);
                        }
                        // 새로운 채팅방에 입장
                        currentRoom = Program.rooms[roomName];
                        currentRoom.clients.Add(this);
                        SendMessage($"채팅방 '{roomName}'에 입장했습니다.");
                        Program.BroadcastMessageToRoom($"[시스템] {nickname}님이 입장했습니다.", currentRoom, this);
                    }
                }
                else
                {
                    SendMessage("올바르지 않은 채팅방 이름입니다.");
                }
            }
            // 채팅방 생성 명령 처리
            else if (message.StartsWith("/create "))
            {
                string roomName = message.Substring(8).Trim();
                if (!string.IsNullOrEmpty(roomName))
                {
                    lock (Program.roomsLock)
                    {
                        if (Program.rooms.ContainsKey(roomName))
                        {
                            SendMessage("동일한 이름의 채팅방이 이미 존재합니다.");
                        }
                        else
                        {
                            // 새로운 채팅방 생성
                            ChatRoom newRoom = new() { roomName = roomName };
                            Program.rooms.Add(roomName, newRoom);

                            // 현재 채팅방에서 나가기
                            if (currentRoom != null)
                            {
                                currentRoom.clients.Remove(this);
                                Program.BroadcastMessageToRoom($"[시스템] {nickname}님이 퇴장했습니다.", currentRoom, this);
                            }

                            // 새로운 채팅방에 입장
                            currentRoom = newRoom;
                            currentRoom.clients.Add(this);
                            SendMessage($"채팅방 '{roomName}'에 입장했습니다.");
                            Program.BroadcastMessageToRoom($"[시스템] {nickname}님이 입장했습니다.", currentRoom, this);
                        }

                    }
                }
                else
                {
                    SendMessage("올바르지 않은 채팅방 이름입니다.");
                }
            }
            // 채팅방 퇴장 명령 처리
            else if (message.Equals("/exit"))
            {
                lock (Program.roomsLock)
                {
                    if (currentRoom != null)
                    {
                        currentRoom.clients.Remove(this);
                        Program.BroadcastMessageToRoom($"[시스템] {nickname}님이 퇴장했습니다.", currentRoom, this);
                        SendMessage($"채팅방 '{currentRoom.roomName}'에서 퇴장했습니다.");
                        currentRoom = null;
                    }
                    else
                    {
                        SendMessage("현재 채팅방에 소속되어 있지 않습니다.");
                    }
                }
            }
            // 채팅방 목록 조회 명령 처리
            else if (message.Equals("/list"))
            {
                foreach (var room in Program.rooms.Values)
                {
                    SendMessage($"채팅방: {room.roomName}, 인원: {room.clients.Count}");
                }
            }
            // 일반 채팅 메시지 처리
            else
            {
                // 메시지 채팅방 내 브로드캐스팅
                if (currentRoom == null)
                {
                    SendMessage("현재 채팅방에 소속되어 있지 않습니다.");
                    return;
                }

                Program.BroadcastMessageToRoom($"[{nickname}] {message}", currentRoom, this);

                // 콘솔에 출력
                Console.WriteLine($"[{currentRoom.roomName} {nickname}] {message}");
            }
        }

        // 메시지 전송 메서드
        public void SendMessage(string message)
        {
            if (isConnected)
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                clientSocket.Send(data);
            }
        }

        // 연결 종료 메서드
        private void CloseConnection()
        {
            if (clientSocket != null)
            {
                try
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException)
                {
                    // 이미 연결이 끊겼을 경우 무시
                }
                finally
                {
                    clientSocket.Close();
                }

                Console.WriteLine($"클라이언트 연결 종료: {nickname}");
            }
        }
    }
}