using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class GameStateDto
{
    public required GameDto Game { get; set; }
    public required List<PlayerDto> Players { get; set; }
    public required List<QueenDto> SleepingQueens { get; set; }
    public required List<QueenDto> AwakenedQueens { get; set; }
    public required List<CardDto> DeckCards { get; set; }
    public required List<CardDto> DiscardPile { get; set; }
    public required List<MoveDto> RecentMoves { get; set; }
    public required PlayerDto? CurrentPlayer { get; set; }
    public required GamePhase CurrentPhase { get; set; }

    // Computed properties
    public bool IsGameOver => Game.Status == GameStatus.Completed || Game.Status == GameStatus.Abandoned;
    public PlayerDto? Winner => Players.OrderByDescending(p => p.Score).FirstOrDefault(p => p.Score >= Game.TargetScore);
    public int CardsInDeck => DeckCards.Count;

    // Helper methods
    public bool CanPlayCard(CardDto card, PlayerDto player)
    {
        if (CurrentPlayer?.Id != player.Id) return false;
        if (!player.Hand.Any(c => c.Id == card.Id)) return false;
        return true;
    }
}

public class GameDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public GamePhase Phase { get; set; }
    public int TargetScore { get; set; }
    public int MaxPlayers { get; set; }
    public GameSettings Settings { get; set; } = GameSettings.Default;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}

public class PlayerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerType Type { get; set; }
    public int Score { get; set; }
    public bool IsCurrentTurn { get; set; }
    public List<CardDto> Hand { get; set; } = [];
    public List<QueenDto> Queens { get; set; } =[];
}

public class GameEventDto
{
    public GameEventType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}

public class QueenDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public Guid? PlayerId { get; set; }
    public bool IsAwake { get; set; }
}

public class CardDto
{
    public Guid Id { get; set; }
    public CardType Type { get; set; }
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}

public class MoveDto
{
    public Guid Id { get; set; }
    public int TurnNumber { get; set; }
    public MoveType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<Guid>? CardIds { get; set; }
    public MoveTargetDto? Target { get; set; }
    public PlayerDto? Player { get; set; }
    public DateTime Timestamp { get; set; }
}

public class MoveTargetDto
{
    public Guid? PlayerId { get; set; }
    public Guid? QueenId { get; set; }
}

// For SignalR/API responses
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> SuccessResponse(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> ErrorResponse(string message) => new() { Success = false, ErrorMessage = message };
}