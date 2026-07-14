using Study_Server.Record;

namespace Study_Server.Network.GameMessages;

public class ReplayListResponse : GameMessage
{
    public List<GameRecordSummary> Games { get; set; }
}
