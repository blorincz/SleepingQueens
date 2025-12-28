using SleepingQueens.Shared.Models.Game.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace SleepingQueens.Shared.Models.Game;

public class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = GenerateGameCode();
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public GamePhase Phase { get; set; } = GamePhase.Setup;
    public int CurrentPlayerIndex { get; set; }
    public int MaxPlayers { get; set; } = 4;
    public int TargetScore { get; set; } = 40;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsPrivate { get; set; }

    // Game Settings as JSON
    [Column(TypeName = "nvarchar(max)")]
    public string SettingsJson { get; set; } = JsonSerializer.Serialize(GameSettings.Default);

    [NotMapped]
    public GameSettings Settings
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<GameSettings>(SettingsJson) ?? GameSettings.Default;
            }
            catch
            {
                return GameSettings.Default;
            }
        }
        set
        {
            SettingsJson = JsonSerializer.Serialize(value);
        }
    }

    // Navigation properties
    public virtual ICollection<Player> Players { get; set; } = [];
    public virtual ICollection<Queen> Queens { get; set; } = [];
    public virtual ICollection<GameCard> DeckCards { get; set; } = [];
    public virtual ICollection<Move> Moves { get; set; } = [];

    private static string GenerateGameCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string([.. Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)])]);
    }

    // Helper methods
    public bool CanStartGame()
    {
        return Status == GameStatus.Waiting &&
               Players.Count >= Settings.MinPlayers &&
               Players.Count <= Settings.MaxPlayers;
    }

    public bool IsFull()
    {
        return Players.Count >= Settings.MaxPlayers;
    }
}