using Study_Server.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Packet;

public static class PacketHandler
{
    public static async Task HandlePacket(TcpSession receiver, IPacket packet)
    {
        // 패킷 ID에 따라 처리 로직을 분기
        switch (packet.Id)
        {
            case PacketId.Chat:
                await HandleChatPacket(receiver, packet);
                break;
            case PacketId.Move:
                await HandleMovePacket(receiver, packet);
                break;
            default:
                Console.WriteLine($"알 수 없는 패킷 ID: {packet.Id}");
                break;
        }
    }

    private static async Task HandleChatPacket(TcpSession receiver, IPacket packet)
    {
        // 채팅 패킷 처리 로직
        Console.WriteLine("채팅 패킷 처리");
        // 에코 서버 처리
        await receiver.SendAsync(PacketSerializer.SerializePacket(packet));
    }

    private static async Task HandleMovePacket(TcpSession receiver, IPacket packet)
    {
        // 이동 패킷 처리 로직
        Console.WriteLine("이동 패킷 처리");
        // 에코 서버 처리
        await receiver.SendAsync(PacketSerializer.SerializePacket(packet));
    }

}
