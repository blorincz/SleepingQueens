namespace SleepingQueens.Shared.Models.DTOs;

public class CreateGameResponseDto
{
    public bool Success { get; set; }
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public string? ErrorMessage { get; set; }
}
