using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Server.GameEngine;

public interface IGameEngine
{
    // Game lifecycle
    Game CreateGame(GameSettings settings, Player creator);
    Task<Game> StartGameAsync(Guid gameId);
    Task<Game> EndGameAsync(Guid gameId, Guid? winnerId = null);
    Task<Game> AbandonGameAsync(Guid gameId);

    // Player management
    Task<Player> AddPlayerAsync(Guid gameId, Player player);
    Task<Player> AddAIPlayerAsync(Guid gameId, AILevel level = AILevel.Medium);
    Task RemovePlayerAsync(Guid gameId, Guid playerId);
    Task UpdatePlayerScoreAsync(Guid playerId, int score);

    // Game actions
    Task<GameActionResult> PlayCardAsync(Guid gameId, Guid playerId, Guid cardId,
        Guid? targetPlayerId = null, Guid? targetQueenId = null);
    Task<GameActionResult> DrawCardAsync(Guid gameId, Guid playerId);
    Task<GameActionResult> EndTurnAsync(Guid gameId, Guid playerId);
    Task<GameActionResult> DiscardCardsAsync(Guid gameId, Guid playerId, IEnumerable<Guid> cardIds);

    // Game state
    Task<GameStateDto> GetGameStateDtoAsync(Guid gameId);
    Task<Player> GetCurrentPlayerAsync(Guid gameId);
    Task<bool> IsValidMoveAsync(Guid gameId, Guid playerId, Guid cardId,
        Guid? targetPlayerId = null, Guid? targetQueenId = null);
    Task<bool> IsGameOverAsync(Guid gameId);
    Task<Player?> CheckForWinnerAsync(Guid gameId);

    // AI operations
    Task<GameActionResult> MakeAIMoveAsync(Guid gameId, Guid aiPlayerId);
    Task ProcessAllAITurnsAsync(Guid gameId);

    // Game settings
    Task UpdateGameSettingsAsync(Guid gameId, GameSettings settings);
    Task<GameSettings> GetGameSettingsAsync(Guid gameId);
}

public record GameActionResult(
    bool Success,
    string Message,
    GameStateDto? UpdatedState = null,
    GameEventDto? GameEvent = null);