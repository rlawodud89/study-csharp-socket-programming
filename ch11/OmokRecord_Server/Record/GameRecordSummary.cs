using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Record;

public class GameRecordSummary
{
    public Guid GameId { get; set; }
    public string BlackPlayer { get; set; }
    public string WhitePlayer { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}
