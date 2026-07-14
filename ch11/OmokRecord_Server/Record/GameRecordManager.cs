using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Record;

public class GameRecordManager
{
    private readonly List<GameRecord> _records = new();

    public void Save(GameRecord record)
    {
        record.EndTime = DateTime.Now;

        _records.Add(record);
    }

    public IReadOnlyList<GameRecord> GetRecords(Guid playerId)
    {
        return _records
            .Where(r =>
                r.BlackPlayerId == playerId ||
                r.WhitePlayerId == playerId)
            .ToList();
    }

    public GameRecord? GetRecord(Guid gameId)
    {
        return _records.FirstOrDefault(r => r.GameId == gameId);
    }
}
