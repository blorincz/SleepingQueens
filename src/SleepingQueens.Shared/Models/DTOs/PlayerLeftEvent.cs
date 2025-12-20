namespace SleepingQueens.Shared.Models.DTOs;

public class PlayerLeftEvent
{
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
}
