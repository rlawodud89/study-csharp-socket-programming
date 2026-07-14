using Study_Server.Record;

namespace Study_Server.Network.GameMessages;

public class ReplayResponse : GameMessage
{
    public List<MoveRecord> Moves { get; set; }
}
