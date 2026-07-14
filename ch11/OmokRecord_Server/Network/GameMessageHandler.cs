using Study_Server.Game;
using Study_Server.Network.GameMessages;
using Study_Server.Record;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Study_Server.Network;

public class GameMessageHandler
{
    private readonly GameRoomManager _roomManager;
    private readonly MatchmakingSystem _matchmakingSystem;
    private readonly PlayerManager _playerManager;
    private readonly GameRecordManager _recordManager;

    public GameMessageHandler(
        GameRoomManager roomManager,
        MatchmakingSystem matchmakingSystem,
        PlayerManager playerManager,
        GameRecordManager recordManager)
    {
        _roomManager = roomManager;
        _matchmakingSystem = matchmakingSystem;
        _playerManager = playerManager;
        _recordManager = recordManager;
    }

    public async Task HandleMessageAsync(ClientSession session, string messageJson)
    {
        // JSON 파싱하여 메시지 타입 확인
        var messageType = JsonDocument.Parse(messageJson)
            .RootElement.GetProperty("MessageType").GetString();

        switch (messageType)
        {
            case "PlaceStoneRequest":
                await HandlePlaceStoneRequestAsync(session, messageJson);
                break;
            case "JoinMatchmakingRequest":
                await HandleJoinMatchmakingRequestAsync(session, messageJson);
                break;
            case "LeaveMatchmakingRequest":
                await HandleLeaveMatchmakingRequestAsync(session, messageJson);
                break;
            case "ResignGameRequest":
                await HandleResignGameRequestAsync(session, messageJson);
                break;

            case "ReplayListRequest":
                await HandleReplayListRequestAsync(session);
                break;

            case "ReplayRequest":
                await HandleReplayRequestAsync(session, messageJson);
                break;

            default:
                // 알 수 없는 메시지 처리
                break;
        }
    }

    private async Task HandlePlaceStoneRequestAsync(ClientSession session, string messageJson)
    {
        var request = JsonSerializer.Deserialize<PlaceStoneRequest>(messageJson);
        var player = _playerManager.GetPlayerBySessionId(session.SessionId);

        if (player == null)
            return;

        var gameRoom = _roomManager.GetPlayerCurrentRoom(player.PlayerId);

        if (gameRoom == null)
        {
            await session.SendAsync(new IllegalMoveNotification
            {
                MessageType = "IllegalMoveNotification",
                Reason = "현재 게임 중이 아닙니다."
            });
            return;
        }

        // 게임 상태 머신을 통해 돌 놓기 시도
        var stateMachine = new OmokGameStateMachine(gameRoom);
        var result = stateMachine.PlaceStone(player, request.X, request.Y);

        if (!result.Success)
        {
            await session.SendAsync(new IllegalMoveNotification
            {
                MessageType = "IllegalMoveNotification",
                Reason = result.ErrorMessage
            });
            return;
        }

        // 게임 상태 업데이트를 양쪽 플레이어에게 전송
        await BroadcastGameStateAsync(gameRoom);

        // 게임이 종료된 경우 결과 처리
        if (result.GameEnded)
        {
            await HandleGameEndAsync(gameRoom, result.Winner, result.IsDraw);
        }
    }

    private async Task HandleReplayListRequestAsync(ClientSession session)
    {
        var player = _playerManager.GetPlayerBySessionId(session.SessionId);

        if (player == null)
            return;

        var response = new ReplayListResponse
        {
            MessageType = "ReplayListResponse"
        };

        foreach (var record in _recordManager.GetRecords(player.PlayerId))
        {
            response.Games.Add(new GameRecordSummary
            {
                GameId = record.GameId,
                BlackPlayer = record.BlackPlayer,
                WhitePlayer = record.WhitePlayer,
                StartTime = record.StartTime,
                EndTime = record.EndTime
            });
        }

        await session.SendAsync(response);
    }

    private async Task HandleReplayRequestAsync(ClientSession session, string messageJson)
    {
        var request = JsonSerializer.Deserialize<ReplayRequest>(messageJson);

        var record = _recordManager.GetRecord(request.GameId);

        if (record == null)
            return;

        var response = new ReplayResponse
        {
            MessageType = "ReplayResponse",
            Moves = record.Moves
        };

        await session.SendAsync(response);
    }

    // 다른 메시지 핸들러 메서드...

    private async Task BroadcastGameStateAsync(GameRoom gameRoom)
    {
        var blackSession = _playerManager.GetPlayerSession(gameRoom.BlackPlayer.PlayerId);
        var whiteSession = _playerManager.GetPlayerSession(gameRoom.WhitePlayer.PlayerId);

        var gameStateUpdate = new GameStateUpdate
        {
            MessageType = "GameStateUpdate",
            Board = gameRoom.Board,
            CurrentTurn = gameRoom.CurrentTurn,
            State = gameRoom.State
        };

        if (blackSession != null)
            await blackSession.SendAsync(gameStateUpdate);

        if (whiteSession != null)
            await whiteSession.SendAsync(gameStateUpdate);
    }

    private async Task HandleGameEndAsync(GameRoom gameRoom, Player winner, bool isDraw)
    {
        // 게임 결과 처리 및 저장...

        _recordManager.Save(gameRoom.Record);
    }
}
