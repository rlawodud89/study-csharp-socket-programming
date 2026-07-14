using Study_Server.Game;
using Study_Server.Network.GameMessages;
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

    public GameMessageHandler(
        GameRoomManager roomManager,
        MatchmakingSystem matchmakingSystem,
        PlayerManager playerManager)
    {
        _roomManager = roomManager;
        _matchmakingSystem = matchmakingSystem;
        _playerManager = playerManager;
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
            case "JoinSpectatorRequest":
                await HandleJoinSpectatorRequestAsync(session, messageJson);
                break;
            case "LeaveSpectatorRequest":
                await HandleLeaveSpectatorRequestAsync(session, messageJson);
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

        // 관전자이면 돌 놓기 실패
        StoneType playerStoneType = player.StoneType;
        if(playerStoneType == StoneType.None)
        {
            await session.SendAsync(new IllegalMoveNotification
            {
                MessageType = "IllegalMoveNotification",
                Reason = "관전자는 돌을 놓을 수 없습니다."
            });
            return;
        }


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

    private async Task HandleJoinSpectatorRequestAsync(ClientSession session, string json)
    {
        var request =
            JsonSerializer.Deserialize<JoinSpectatorRequest>(json);

        var room =
            _roomManager.GetRoom(request.RoomId);

        if (room == null)
            return;

        var player =
            _playerManager.GetPlayerBySessionId(session.SessionId);

        room.AddSpectator(player);

        player.State = PlayerState.Spectating;

        // 현재 보드 전송
        await session.SendAsync(new GameStateUpdate
        {
            MessageType = "GameStateUpdate",
            Board = room.Board,
            CurrentTurn = room.CurrentTurn,
            State = room.State
        });
    }

    private async Task HandleLeaveSpectatorRequestAsync(ClientSession session, string json)
    {
        var request =
            JsonSerializer.Deserialize<LeaveSpectatorRequest>(json);

        var room =
            _roomManager.GetRoom(request.RoomId);

        if (room == null)
            return;

        var player =
            _playerManager.GetPlayerBySessionId(session.SessionId);

        room.RemoveSpectator(player);

        player.State = PlayerState.Online;
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

        // 관전자에게도 전송
        foreach (var spectator in gameRoom.Spectators)
        {
            var session =
                _playerManager.GetPlayerSession(spectator.PlayerId);

            if (session != null)
                await session.SendAsync(gameStateUpdate);
        }
    }

    private async Task HandleGameEndAsync(GameRoom gameRoom, Player winner, bool isDraw)
    {
        // 게임 결과 처리 및 저장...
    }
}
