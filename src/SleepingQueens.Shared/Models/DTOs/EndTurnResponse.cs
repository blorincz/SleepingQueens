namespace SleepingQueens.Shared.Models.DTOs;

public class EndTurnResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }  // Should be GameStateDto
}
