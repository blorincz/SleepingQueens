using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class ActiveGameInfo
{
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public GameStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public bool CanJoin { get; set; }
    public TimeSpan? TimeRemaining { get; set; }
    public string GameMode { get; set; } = string.Empty;
}
