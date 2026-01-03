using SleepingQueens.Shared.Models.DTOs;

namespace SleepingQueens.Shared.Models.Events;

public class GameStartedEvent
{
    public Guid GameId { get; set; }
    public DateTime StartedAt { get; set; }
    public List<PlayerDto> Players { get; set; } = [];
}
