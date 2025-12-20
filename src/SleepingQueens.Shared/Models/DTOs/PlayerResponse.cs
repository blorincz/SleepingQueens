using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class PlayerResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerType Type { get; set; }
    public int Score { get; set; }
    public bool IsCurrentTurn { get; set; }
    public List<QueenResponse> Queens { get; set; } = [];
    public int HandSize { get; set; }
}
