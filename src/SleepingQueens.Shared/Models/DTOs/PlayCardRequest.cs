namespace SleepingQueens.Shared.Models.DTOs;

public class PlayCardRequest
{
    public Guid GameId { get; set; }
    public Guid CardId { get; set; }
    public Guid? TargetPlayerId { get; set; }
    public Guid? TargetQueenId { get; set; }
}
