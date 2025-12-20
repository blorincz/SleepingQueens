namespace SleepingQueens.Shared.Models.DTOs;

public class CreateGameResult
{
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
}
