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
    public virtual ICollection<PlayerCard> PlayerCards { get; set; } = new List<PlayerCard>();
    public virtual ICollection<GameCard> GameCards { get; set; } = new List<GameCard>();
}

public enum CardType
{
    King = 1,
    Queen = 2,
    Knight = 3,
    Dragon = 4,
    SleepingPotion = 5,
    Jester = 6,
    Number = 7
}