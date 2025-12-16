namespace SleepingQueens.Shared.Models.Game;

public class Move
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TurnNumber { get; set; }
    public MoveType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? CardIds { get; set; } // JSON array of card IDs
    public string? TargetData { get; set; } // JSON for target info
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Guid GameId { get; set; }
    public virtual Game Game { get; set; } = null!;
    public Guid PlayerId { get; set; }
    public virtual Player Player { get; set; } = null!;
}