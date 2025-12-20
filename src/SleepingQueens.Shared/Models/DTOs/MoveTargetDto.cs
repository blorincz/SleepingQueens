namespace SleepingQueens.Shared.Models.DTOs;

public class MoveTargetDto
{
    public Guid? PlayerId { get; set; }
    public Guid? QueenId { get; set; }
}
