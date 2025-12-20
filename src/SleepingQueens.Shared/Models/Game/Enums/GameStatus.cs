using System.Text.Json.Serialization;

namespace SleepingQueens.Shared.Models.Game.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GameStatus
{
    Waiting = 1,     // Game created, waiting for players
    Active = 2,      // Game in progress
    Paused = 3,      // Game temporarily paused
    Completed = 4,   // Game finished normally
    Abandoned = 5    // Game abandoned before completion
}