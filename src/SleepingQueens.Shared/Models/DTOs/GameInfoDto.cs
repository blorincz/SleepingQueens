using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

// Add these DTOs if they don't exist in Shared
public class GameInfoDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public GameStatus Status { get; set; }
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public DateTime CreatedAt { get; set; }
}
