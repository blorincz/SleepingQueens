namespace SleepingQueens.Shared.Models.Game;

public class GameCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Position { get; set; } // Position in deck/discard
    public CardLocation Location { get; set; } = CardLocation.Deck;

    // Foreign keys
    public Guid GameId { get; set; }
    public Guid CardId { get; set; }

    // Navigation properties
    public virtual Game Game { get; set; } = null!;
    public virtual Card Card { get; set; } = null!;
}