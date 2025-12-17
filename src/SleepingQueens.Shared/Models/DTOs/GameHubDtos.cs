using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

// SignalR Hub Method DTOs
public class CreateGameRequest
{
    public string PlayerName { get; set; } = string.Empty;
    public GameSettings Settings { get; set; } = GameSettings.Default;
}

public class CreateGameResponse : ApiResponse<CreateGameResult> { }

public class CreateGameResult
{
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
}

public class JoinGameRequest
{
    public string GameCode { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
}

public class JoinGameResponse : ApiResponse<JoinGameResult> { }

public class JoinGameResult
{
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public GameStateDto GameState { get; set; } = null!;
}

public class PlayCardRequest
{
    public Guid GameId { get; set; }
    public Guid CardId { get; set; }
    public Guid? TargetPlayerId { get; set; }
    public Guid? TargetQueenId { get; set; }
}

public class PlayCardResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }  // Should be GameStateDto
}

public class DrawCardResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }  // Should be GameStateDto
}

public class EndTurnResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }  // Should be GameStateDto
}

public class GetGameStateResponse : ApiResponse<GameStateDto> { }

// Events
public class PlayerJoinedEvent
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TotalPlayers { get; set; }
}

public class PlayerLeftEvent
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
}

public class GameStartedEvent
{
    public Guid GameId { get; set; }
    public DateTime StartedAt { get; set; }
    public List<PlayerInfo> Players { get; set; } = [];
}

public class PlayerInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCurrentTurn { get; set; }
}

public class ChatMessageDto
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class StartGameResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ActiveGameInfo
{
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public GameStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMessage
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}