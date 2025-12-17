namespace SleepingQueens.Shared.Models.Game.Enums;

public enum GamePhase
{
    Setup = 1,       // Setting up game, adding players
    Playing = 2,     // Main game phase
    Scoring = 3,     // Calculating final scores
    Ended = 4        // Game ended
}