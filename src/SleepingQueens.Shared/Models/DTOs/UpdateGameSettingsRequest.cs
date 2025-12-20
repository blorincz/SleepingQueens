using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Shared.Models.DTOs;

public class UpdateGameSettingsRequest
{
    public GameSettings Settings { get; set; } = GameSettings.Default;
}
