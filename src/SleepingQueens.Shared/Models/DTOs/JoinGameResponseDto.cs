namespace SleepingQueens.Shared.Models.DTOs;

public class JoinGameResponseDto
{
    public bool Success { get; set; }
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public string? ErrorMessage { get; set; }
}