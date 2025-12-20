// SleepingQueens.Client/Services/GameStateService.cs
using SleepingQueens.Client.Events;
using SleepingQueens.Shared.Models.DTOs;

namespace SleepingQueens.Client.Services;

public interface IGameStateService
{
    GameStateDto? CurrentGameState { get; }
    Guid? CurrentPlayerId { get; }
    Guid? CurrentGameId { get; }
    bool IsPlayerTurn { get; }

    IAsyncEvent<GameStateDto> OnGameStateChanged { get; }

    Task UpdateGameStateAsync(GameStateDto gameState);
    void SetPlayerId(Guid playerId);
    void SetGameId(Guid gameId);
    Task ClearAsync();
}

public class GameStateService : IGameStateService
{
    private GameStateDto? _currentGameState;
    private Guid? _currentPlayerId;
    private Guid? _currentGameId;

    public GameStateDto? CurrentGameState => _currentGameState;
    public Guid? CurrentPlayerId => _currentPlayerId;
    public Guid? CurrentGameId => _currentGameId;

    public bool IsPlayerTurn
    {
        get
        {
            if (CurrentGameState == null || !CurrentPlayerId.HasValue)
                return false;

            return CurrentGameState.CurrentPlayer?.Id == CurrentPlayerId.Value;
        }
    }

    public IAsyncEvent<GameStateDto> OnGameStateChanged { get; }

    public GameStateService(ILogger<GameStateService> logger)
    {
        OnGameStateChanged = new AsyncEvent<GameStateDto>(logger);
    }

    public async Task UpdateGameStateAsync(GameStateDto gameState)
    {
        _currentGameState = gameState;

        if (_currentGameId == null && gameState.Game != null)
        {
            _currentGameId = gameState.Game.Id;
        }

        await OnGameStateChanged.InvokeAsync(gameState);
    }

    public void SetPlayerId(Guid playerId)
    {
        _currentPlayerId = playerId;
    }

    public void SetGameId(Guid gameId)
    {
        _currentGameId = gameId;
    }

    public async Task ClearAsync()
    {
        _currentGameState = null;
        _currentPlayerId = null;
        _currentGameId = null;

        // Notify that state has been cleared
        await OnGameStateChanged.InvokeAsync(null!); // Pass null to indicate cleared state
    }
}