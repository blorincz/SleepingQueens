using SleepingQueens.Shared.Models.DTOs;

public class DrawCardResult
{
    public Guid CardId { get; set; }
    public string CardName { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }
}