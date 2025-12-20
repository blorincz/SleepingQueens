using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

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
