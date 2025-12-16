using System.Text.Json;
using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game;

public class GameSettings
{
    // Player settings
    public int MaxPlayers { get; set; } = 4;
    public int MinPlayers { get; set; } = 2;

    // Game rules
    public int TargetScore { get; set; } = 40;
    public int StartingHandSize { get; set; } = 5;
    public bool EnableSleepingQueens { get; set; } = true;
    public bool EnableSpecialCards { get; set; } = true;

    // AI settings
    public bool AllowAI { get; set; } = true;
    public AILevel DefaultAILevel { get; set; } = AILevel.Medium;
    public int AICount { get; set; } = 0; // Number of AI players to add automatically

    // Game variations
    public bool AllowCardStealing { get; set; } = true;
    public bool AllowQueenStealing { get; set; } = true;
    public bool AllowDragonProtection { get; set; } = true;
    public bool AllowSleepingPotions { get; set; } = true;
    public bool AllowJester { get; set; } = true;
    public bool AllowSelfPotion { get; set; } = false;
    public bool RequireExactScoreToWin { get; set; } = false;

    // Turn settings
    public TimeSpan TurnTimeLimit { get; set; } = TimeSpan.FromMinutes(2);
    public bool EnableTurnTimer { get; set; } = false;
    public bool AutoSkipInactivePlayers { get; set; } = true;

    // UI/Client settings
    public bool EnableSound { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool EnableCardDragDrop { get; set; } = true;
    public bool EnableTutorial { get; set; } = false;

    // Deck composition
    public int NumberCardCountPerValue { get; set; } = 4;
    public int KingCardCount { get; set; } = 4;
    public int KnightCardCount { get; set; } = 3;
    public int DragonCardCount { get; set; } = 3;
    public int SleepingPotionCount { get; set; } = 4;
    public int JesterCardCount { get; set; } = 5;

    // Scoring variations
    public bool UseStandardScoring { get; set; } = true;
    public bool BonusForMostQueens { get; set; } = false;
    public bool BonusForSpecialCardCombos { get; set; } = false;

    // Network/Online settings
    public bool IsPublic { get; set; } = false;
    public bool AllowSpectators { get; set; } = false;
    public bool EnableChat { get; set; } = true;

    // Validation
    public bool Validate()
    {
        if (MaxPlayers < MinPlayers || MaxPlayers > 6)
            return false;

        if (TargetScore < 10 || TargetScore > 100)
            return false;

        if (StartingHandSize < 3 || StartingHandSize > 7)
            return false;

        if (AICount < 0 || AICount > MaxPlayers - 1)
            return false;

        return true;
    }

    public static GameSettings Default => new();

    public static GameSettings QuickStart => new()
    {
        MaxPlayers = 4,
        TargetScore = 40,
        EnableSound = true,
        EnableAnimations = true
    };

    public static GameSettings SoloVsAI => new()
    {
        MaxPlayers = 4,
        TargetScore = 40,
        AICount = 3,
        DefaultAILevel = AILevel.Medium,
        EnableSound = true
    };

    public static GameSettings Hardcore => new()
    {
        MaxPlayers = 4,
        TargetScore = 50,
        AllowSleepingPotions = true,
        EnableTurnTimer = true,
        TurnTimeLimit = TimeSpan.FromSeconds(30),
        RequireExactScoreToWin = true
    };

    public static GameSettings FamilyFriendly => new()
    {
        MaxPlayers = 6,
        TargetScore = 30,
        AllowQueenStealing = false,
        EnableTutorial = true,
        EnableAnimations = true,
        EnableSound = true
    };
}

public enum AILevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
    Expert = 4
}

// JSON converter for TimeSpan (optional but helpful)
public class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return TimeSpan.FromSeconds(reader.GetInt64());

        if (reader.TokenType == JsonTokenType.String)
            return TimeSpan.Parse(reader.GetString()!);

        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.TotalSeconds);
    }
}