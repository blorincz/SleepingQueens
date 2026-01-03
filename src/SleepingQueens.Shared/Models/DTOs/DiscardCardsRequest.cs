namespace SleepingQueens.Shared.Models.DTOs;

public class DiscardCardsRequest
{
    public Guid GameId { get; set; }
    public IEnumerable<Guid>? Cards { get; set; }
}
