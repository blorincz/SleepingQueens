namespace SleepingQueens.Shared.Models.DTOs;

public class DrawCardResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? CardId { get; set; }
    public string? CardName { get; set; }
    public GameStateDto? GameState { get; set; }  // Should be GameStateDto
}
