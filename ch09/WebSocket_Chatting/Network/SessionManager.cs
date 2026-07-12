using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_WebServer.Network;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

    public void Add(ClientSession session)
    {
        _sessions.TryAdd(session.ConnectionId, session);
    }

    public void Remove(string id)
    {
        _sessions.TryRemove(id, out _);
    }

    public ClientSession? Find(string id)
    {
        _sessions.TryGetValue(id, out var session);
        return session;
    }
}
