using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Game;

public class MoveResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public bool GameEnded { get; set; }
    public Player Winner { get; set; }
    public bool IsDraw { get; set; }
}