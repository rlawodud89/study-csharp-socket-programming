using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Game;

public class GameRoom
{
    public Guid RoomId { get; private set; }
    public Player BlackPlayer { get; private set; }
    public Player WhitePlayer { get; private set; }

    private readonly List<Player> _spectators = new();

    public StoneType[,] Board { get; private set; }
    public GameState State { get; set; }
    public StoneType CurrentTurn { get; set; }
    public DateTime LastMoveTime { get; set; }
    public bool IsRenjuRule { get; set; }

    // 게임 룸 생성
    public GameRoom(Player player1, Player player2, bool isRenjuRule = false)
    {
        RoomId = Guid.NewGuid();

        // 랜덤하게 흑백 결정
        if (Random.Shared.Next(2) == 0)
        {
            BlackPlayer = player1;
            WhitePlayer = player2;
        }
        else
        {
            BlackPlayer = player2;
            WhitePlayer = player1;
        }

        Board = new StoneType[15, 15];
        State = GameState.Waiting;
        CurrentTurn = StoneType.Black;
        LastMoveTime = DateTime.UtcNow;
        IsRenjuRule = isRenjuRule;
    }

    // 기타 메서드...

    public IReadOnlyList<Player> Spectators => _spectators;

    public void AddSpectator(Player player)
    {
        if (!_spectators.Contains(player))
            _spectators.Add(player);
    }

    public void RemoveSpectator(Player player)
    {
        _spectators.Remove(player);
    }
}
