namespace SleepingQueens.Shared.Models.DTOs;

public class GameStartedEvent
{
    public Guid GameId { get; set; }
    public DateTime StartedAt { get; set; }
    public List<PlayerInfo> Players { get; set; } = [];
}
