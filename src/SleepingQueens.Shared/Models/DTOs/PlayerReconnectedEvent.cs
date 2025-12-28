namespace SleepingQueens.Shared.Models.DTOs;

public class PlayerReconnectedEvent
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public DateTime ReconnectedAt { get; set; } = DateTime.UtcNow;
}
