namespace SleepingQueens.Shared.Models.DTOs;

public class PlayerInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCurrentTurn { get; set; }
}
