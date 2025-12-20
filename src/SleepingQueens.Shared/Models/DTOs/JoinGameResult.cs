namespace SleepingQueens.Shared.Models.DTOs;

public class JoinGameResult
{
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public GameStateDto GameState { get; set; } = null!;
}
