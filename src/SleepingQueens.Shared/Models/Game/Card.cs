using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.Game;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public CardType Type { get; set; }
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public bool IsSpecialCard => Type != CardType.Number;

    // Navigation properties
    public virtual ICollection<PlayerCard> PlayerCards { get; set; } = [];
    public virtual ICollection<GameCard> GameCards { get; set; } = [];
}