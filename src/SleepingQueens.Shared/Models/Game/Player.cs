using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.Game;

public class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PlayerType Type { get; set; } = PlayerType.Human;
    public int Score { get; set; }
    public bool IsCurrentTurn { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsAI { get; set; } = false;
    public AILevel AILevel { get; set; } = AILevel.Medium;
    public bool IsConnected { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }
    public DateTime? LastReconnectedAt { get; set; }
    public bool IsAIControlled { get; set; } // For when AI takes over during disconnect

    // Navigation properties
    public Guid GameId { get; set; }
    public virtual Game Game { get; set; } = null!;
    public virtual ICollection<Queen> Queens { get; set; } = [];
    public virtual ICollection<PlayerCard> PlayerCards { get; set; } = [];
    public virtual ICollection<Move> Moves { get; set; } = [];
}