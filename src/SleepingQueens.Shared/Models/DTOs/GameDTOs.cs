using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class GameResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public GamePhase Phase { get; set; }
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public GameSettings Settings { get; set; } = GameSettings.Default;
    public DateTime CreatedAt { get; set; }
    public List<PlayerResponse> Players { get; set; } = [];
}

public class PlayerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerType Type { get; set; }
    public int Score { get; set; }
    public bool IsCurrentTurn { get; set; }
    public List<QueenResponse> Queens { get; set; } = [];
    public int HandSize { get; set; }
}

public class QueenResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PointValue { get; set; }
    public string ImagePath { get; set; } = string.Empty;
}

public class UpdateGameSettingsRequest
{
    public GameSettings Settings { get; set; } = GameSettings.Default;
}