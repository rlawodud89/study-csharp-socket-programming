using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Study_Server.Game;

public class OmokGameStateMachine
{
    private readonly GameRoom _gameRoom;

    public OmokGameStateMachine(GameRoom gameRoom)
    {
        _gameRoom = gameRoom;
    }

    public void StartGame()
    {
        if (_gameRoom.State != GameState.Waiting)
            throw new InvalidOperationException("게임이 이미 시작되었습니다.");

        _gameRoom.State = GameState.Playing;
        // 게임 시작 이벤트 발생
    }

    public MoveResult PlaceStone(Player player, int x, int y)
    {
        // 현재 차례인지 확인
        StoneType currentPlayerType = GetPlayerStoneType(player);
        if (currentPlayerType != _gameRoom.CurrentTurn)
            return new MoveResult { Success = false, ErrorMessage = "당신의 차례가 아닙니다." };

        // 게임 상태 확인
        if (_gameRoom.State != GameState.Playing)
            return new MoveResult { Success = false, ErrorMessage = "게임이 진행 중이 아닙니다." };

        // 해당 위치가 유효한지 확인
        if (x < 0 || x >= 15 || y < 0 || y >= 15)
            return new MoveResult { Success = false, ErrorMessage = "유효하지 않은 위치입니다." };

        // 이미 돌이 놓여있는지 확인
        if (_gameRoom.Board[x, y] != StoneType.None)
            return new MoveResult { Success = false, ErrorMessage = "이미 돌이 놓여있는 위치입니다." };

        // 렌주룰 체크 (흑돌의 경우)
        if (_gameRoom.IsRenjuRule && currentPlayerType == StoneType.Black)
        {
            if (IsThreeThree(x, y, currentPlayerType))
                return new MoveResult { Success = false, ErrorMessage = "삼삼 금수입니다." };

            if (IsFourFour(x, y, currentPlayerType))
                return new MoveResult { Success = false, ErrorMessage = "사사 금수입니다." };

            if (IsOverline(x, y, currentPlayerType))
                return new MoveResult { Success = false, ErrorMessage = "장목(6목 이상)은 금지됩니다." };
        }

        // 돌 배치
        _gameRoom.Board[x, y] = currentPlayerType;
        _gameRoom.LastMoveTime = DateTime.UtcNow;

        // 승리 조건 확인
        if (CheckWin(x, y, currentPlayerType))
        {
            _gameRoom.State = GameState.Finished;
            return new MoveResult
            {
                Success = true,
                GameEnded = true,
                Winner = player
            };
        }

        // 무승부 확인 (보드가 가득 찬 경우)
        if (IsBoardFull())
        {
            _gameRoom.State = GameState.Finished;
            return new MoveResult
            {
                Success = true,
                GameEnded = true,
                IsDraw = true
            };
        }

        // 차례 변경
        _gameRoom.CurrentTurn = currentPlayerType == StoneType.Black ? StoneType.White : StoneType.Black;

        return new MoveResult { Success = true };
    }

    private StoneType GetPlayerStoneType(Player player)
    {
        if (player.PlayerId == _gameRoom.BlackPlayer.PlayerId)
            return StoneType.Black;
        if (player.PlayerId == _gameRoom.WhitePlayer.PlayerId)
            return StoneType.White;
        return StoneType.None;
    }

    // 승리 조건, 렌주룰, 기타 게임 로직 체크 메서드...

    private bool CheckWin(int x, int y, StoneType stoneType)
    {
        // 가로, 세로, 대각선 네 방향으로 연속된 돌 검사
        int[] dx = { 1, 0, 1, 1 };  // 가로, 세로, 대각선(\), 대각선(/)
        int[] dy = { 0, 1, 1, -1 };

        for (int dir = 0; dir < 4; dir++)
        {
            int count = 1;  // 현재 놓은 돌부터 시작

            // 정방향 확인
            for (int i = 1; i <= 5; i++)
            {
                int nx = x + dx[dir] * i;
                int ny = y + dy[dir] * i;

                if (nx < 0 || nx >= 15 || ny < 0 || ny >= 15 || _gameRoom.Board[nx, ny] != stoneType)
                    break;

                count++;
            }

            // 역방향 확인
            for (int i = 1; i <= 5; i++)
            {
                int nx = x - dx[dir] * i;
                int ny = y - dy[dir] * i;

                if (nx < 0 || nx >= 15 || ny < 0 || ny >= 15 || _gameRoom.Board[nx, ny] != stoneType)
                    break;

                count++;
            }

            // 렌주룰이 적용된 경우 흑돌은 정확히 5개, 백돌은 5개 이상
            if (_gameRoom.IsRenjuRule && stoneType == StoneType.Black)
            {
                if (count == 5)
                    return true;
            }
            else
            {
                // 렌주룰이 적용되지 않거나 백돌인 경우 5개 이상
                if (count >= 5)
                    return true;
            }
        }

        return false;
    }
}

