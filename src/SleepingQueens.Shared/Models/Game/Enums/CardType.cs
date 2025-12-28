using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardType
{
    King = 1,
    Queen = 2,
    Knight = 3,
    Dragon = 4,
    SleepingPotion = 5,
    Wand = 6,
    Jester = 7,
    Number = 8
}
