using System;
using System.Net;
using System.Net.Sockets;

using Study_Server.Network;
using Study_Server.Packet;

class Program
{
    async static Task Main(string[] args)
    {
        TcpServer server = new TcpServer(
            port: 8888,
            sessionManager: new SessionManager(),
            packetProcessor: new PacketProcessor()
        );

        await server.StartAsync();
    }
}