using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardLocation
{
    Deck = 1,
    Discard = 2,
    PlayerHand = 3,
    InPlay = 4
}