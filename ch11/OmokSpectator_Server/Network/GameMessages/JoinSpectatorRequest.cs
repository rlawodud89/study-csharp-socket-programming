using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Network.GameMessages;

public class JoinSpectatorRequest : GameMessage
{
    public int RoomId { get; set; }
}
