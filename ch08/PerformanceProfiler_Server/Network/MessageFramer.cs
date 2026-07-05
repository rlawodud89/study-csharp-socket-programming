using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Network;

public class MessageFramer
{
    private readonly NetworkStream _stream;
    private readonly byte[] _lengthBuffer = new byte[4];
    private readonly byte[] _messageBuffer = new byte[1024 * 64]; // 64KB 최대 메시지 크기

    public MessageFramer(NetworkStream stream)
    {
        _stream = stream;
    }

    public async Task<byte[]> ReceiveMessageAsync()
    {
        // 1. Length 읽기 (전체 패킷 크기)
        int bytesRead = 0;
        while (bytesRead < 4)
        {
            int read = await _stream.ReadAsync(_lengthBuffer, bytesRead, 4 - bytesRead);
            if (read == 0)
                throw new EndOfStreamException("연결이 닫혔습니다.");

            bytesRead += read;
        }

        int totalLength = BitConverter.ToInt32(_lengthBuffer, 0);

        if (totalLength <= 4 || totalLength > _messageBuffer.Length)
            throw new InvalidDataException($"메시지 길이가 너무 큽니다: {totalLength}");

        // 2. 남은 데이터 길이
        int bodyLength = totalLength - 4;

        bytesRead = 0;
        while (bytesRead < bodyLength)
        {
            int read = await _stream.ReadAsync(_messageBuffer, bytesRead, bodyLength - bytesRead);
            if (read == 0)
                throw new EndOfStreamException("연결이 닫혔습니다.");

            bytesRead += read;
        }

        // 3. 최종 패킷 조립
        byte[] packet = new byte[totalLength];

        // Length 포함 복원
        Buffer.BlockCopy(_lengthBuffer, 0, packet, 0, 4);
        Buffer.BlockCopy(_messageBuffer, 0, packet, 4, bodyLength);

        return packet;
    }
}
