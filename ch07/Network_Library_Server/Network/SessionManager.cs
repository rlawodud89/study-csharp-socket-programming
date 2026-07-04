using System.Collections.Concurrent;

namespace Study_Server.Network;

public class SessionManager
{
    private readonly ConcurrentDictionary<int, TcpSession> _sessions = new();

    // 세션 등록
    public bool AddSession(int sessionId, TcpSession session)
    {
        bool result = _sessions.TryAdd(sessionId, session);

        if (result)
        {
            Console.WriteLine($"Session Add : {sessionId}");
        }

        return result;
    }

    // 세션 제거
    public bool Remove(TcpSession session)
    {
        return Remove(session.SessionId);
    }

    // 세션 제거
    public bool Remove(int sessionId)
    {
        bool result = _sessions.TryRemove(sessionId, out _);

        if (result)
        {
            Console.WriteLine($"Session Remove : {sessionId}");
        }

        return result;
    }

    // 세션 조회
    public TcpSession? Find(int sessionId)
    {
        _sessions.TryGetValue(sessionId, out TcpSession? session);
        return session;
    }

    // 현재 연결 수
    public int Count => _sessions.Count;

    // 전체 세션
    public IReadOnlyCollection<TcpSession> Sessions
        => _sessions.Values.ToArray();
}