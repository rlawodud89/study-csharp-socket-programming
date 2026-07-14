using Study_Server.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Record;

public class MoveRecord
{
    public int Turn { get; set; }

    public StoneType Stone { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public DateTime Time { get; set; }
}
