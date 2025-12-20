namespace SleepingQueens.Shared.Models.DTOs;

// Events
public class PlayerJoinedEvent
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TotalPlayers { get; set; }
}
