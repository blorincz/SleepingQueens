using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Server.Data.Repositories;

public interface IGameRepository
{
    // Game operations
    Task<Game?> GetByIdAsync(Guid id);
    Task<Game?> GetByCodeAsync(string code);
    Task<IEnumerable<Game>> GetActiveGamesAsync();
    Task<IEnumerable<Game>> GetGamesByStatusAsync(GameStatus status);
    Task<IEnumerable<Game>> GetGamesByPlayerIdAsync(Guid playerId);
    Task<bool> CodeExistsAsync(string code);
    Task<string> GenerateUniqueCodeAsync();

    // Player operations
    Task<Player?> GetPlayerAsync(Guid playerId);
    Task<Player> AddPlayerAsync(Guid gameId, Player player);
    Task RemovePlayerAsync(Guid playerId);
    Task UpdatePlayerTurnAsync(Guid gameId, Guid playerId, bool isCurrentTurn);
    Task<IEnumerable<Player>> GetPlayersInGameAsync(Guid gameId);

    // Card operations
    Task<IEnumerable<Card>> GetPlayerHandAsync(Guid playerId);
    Task AddCardToPlayerHandAsync(Guid playerId, Guid cardId);
    Task RemoveCardFromPlayerHandAsync(Guid playerId, Guid cardId);
    Task<GameCard?> DrawCardFromDeckAsync(Guid gameId);
    Task ReturnCardToDeckAsync(Guid gameId, Guid cardId);
    Task DiscardCardAsync(Guid gameId, Guid cardId);
    Task<IEnumerable<GameCard>> GetDeckCardsAsync(Guid gameId);
    Task<IEnumerable<GameCard>> GetDiscardPileAsync(Guid gameId);

    // Queen operations
    Task<IEnumerable<Queen>> GetSleepingQueensAsync(Guid gameId);
    Task<IEnumerable<Queen>> GetPlayerQueensAsync(Guid playerId);
    Task<IEnumerable<Queen>> GetQueensForGameAsync(Guid gameId);
    Task<Queen?> GetQueenByIdAsync(Guid queenId);
    Task TransferQueenAsync(Guid queenId, Guid toPlayerId);
    Task PutQueenToSleepAsync(Guid queenId);
    Task WakeQueenAsync(Guid queenId, Guid playerId);

    // Move operations
    Task<Move> RecordMoveAsync(Move move);
    Task<List<Move>> GetGameMovesAsync(Guid gameId, int limit = 50);
    Task<int> GetNextTurnNumberAsync(Guid gameId);

    // Complex operations
    Task<GameStateDto> GetGameStateDtoAsync(Guid gameId);
    Task InitializeNewGameAsync(Game game, Player firstPlayer);
    Task ShuffleDeckAsync(Guid gameId);
    Task<bool> CheckForWinnerAsync(Guid gameId);
    Task EndGameAsync(Guid gameId, Guid? winnerId = null);
    Task UpdateGameSettingsAsync(Guid gameId, GameSettings settings);
    Task UpdateAsync(Game game);
}