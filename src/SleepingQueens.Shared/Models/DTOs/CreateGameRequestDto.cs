using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Shared.Models.DTOs;

public class CreateGameRequestDto
{
    public string PlayerName { get; set; } = string.Empty;
    public GameSettings Settings { get; set; } = GameSettings.Default;
}
