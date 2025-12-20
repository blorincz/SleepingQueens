using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameEventType
{
    CardPlayed,
    QueenWoken,
    QueenStolen,
    DragonBlocked,
    PotionUsed,
    TurnEnded,
    GameStarted,
    GameEnded,
    PlayerJoined,
    PlayerLeft,
    ChatMessage
}
