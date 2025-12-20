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
