namespace SleepingQueens.Shared.Models.Events;

public class PlayerLeftEvent
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public bool CanReconnect { get; set; }
    public string? Message { get; set; }
}
