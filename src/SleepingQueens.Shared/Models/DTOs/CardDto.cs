using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class CardDto
{
    public Guid Id { get; set; }
    public CardType Type { get; set; }
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}
