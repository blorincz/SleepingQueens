using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerType
{
    Human = 1,
    AI_Easy = 2,
    AI_Medium = 3,
    AI_Hard = 4
}