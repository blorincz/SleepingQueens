using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AILevel
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
    Expert = 4
}
