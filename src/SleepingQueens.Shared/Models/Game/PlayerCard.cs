namespace SleepingQueens.Shared.Models.Game;

public class PlayerCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int HandPosition { get; set; }
    public bool IsSelected { get; set; }

    // Foreign keys
    public Guid PlayerId { get; set; }
    public Guid CardId { get; set; }

    // Navigation properties
    public virtual Player Player { get; set; } = null!;
    public virtual Card Card { get; set; } = null!;
}