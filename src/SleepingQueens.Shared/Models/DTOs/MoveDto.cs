using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class MoveDto
{
    public Guid Id { get; set; }
    public int TurnNumber { get; set; }
    public MoveType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<Guid>? CardIds { get; set; }
    public MoveTargetDto? Target { get; set; }
    public PlayerDto? Player { get; set; }
    public DateTime Timestamp { get; set; }
}
