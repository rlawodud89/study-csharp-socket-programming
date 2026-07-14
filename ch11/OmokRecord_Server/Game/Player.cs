using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Game;

public class Player
{
    public Guid PlayerId { get; private set; }
    public string Username { get; private set; }
    public int Rating { get; set; }
    public PlayerState State { get; set; }

    // 생성자 및 기타 메서드...
}
