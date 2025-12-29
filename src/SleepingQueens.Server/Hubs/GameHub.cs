using Microsoft.AspNetCore.SignalR;
using SleepingQueens.Server.GameEngine;
using SleepingQueens.Server.Logging;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Server.Hubs;

public class GameHub(
    IGameEngine gameEngine,
    ILogger<GameHub> logger) : Hub
{
    private readonly IGameEngine _gameEngine = gameEngine;
    private readonly ILogger<GameHub> _logger = logger;
    private static readonly Dictionary<string, Guid> _connectionPlayerMap = [];
    private static readonly Dictionary<Guid, string> _playerConnectionMap = [];
    private static readonly Dictionary<Guid, DateTime> _disconnectionTimes = [];

    // ========== CONNECTION MANAGEMENT ==========

    public override async Task OnConnectedAsync()
    {
        _logger.LogClientConnected(Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Handle player disconnect
        if (_connectionPlayerMap.TryGetValue(Context.ConnectionId, out var playerId))
        {
            await HandlePlayerDisconnect(playerId);
        }

        _logger.LogClientDisconnected(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ========== GAME MANAGEMENT ==========

    public async Task<ApiResponse<CreateGameResult>> CreateGame(CreateGameRequest request)
    {
        try
        {
            _logger.LogGameCreatedForPlayer(request.PlayerName);

            var creator = new Player
            {
                Name = request.PlayerName,
                ConnectionId = Context.ConnectionId
            };

            var game = await _gameEngine.CreateGame(request.Settings, creator);

            // Map connection to player
            await MapPlayerConnectionAsync(creator.Id, Context.ConnectionId);

            // Add to game group
            await Groups.AddToGroupAsync(Context.ConnectionId, game.Id.ToString());

            var gameStateDto = await _gameEngine.GetGameStateDtoAsync(game.Id);

            return ApiResponse<CreateGameResult>.SuccessResponse(new CreateGameResult
            {
                GameId = game.Id,
                GameCode = game.Code,
                PlayerId = creator.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating game");
            return ApiResponse<CreateGameResult>.ErrorResponse(ex.Message);
        }
    }

    public async Task<ApiResponse<JoinGameResultDto>> JoinGame(JoinGameRequest request)
    {
        try
        {
            var playerId = GetPlayerId();
            var result = await _gameEngine.JoinGameAsync(request.GameCode, request.PlayerName, Context.ConnectionId, playerId);

            if (result.Success)
            {
                await MapPlayerConnectionAsync(result.JoinedPlayerId, Context.ConnectionId);
                await Groups.AddToGroupAsync(Context.ConnectionId, result.GameId.ToString());
                await NotifyPlayerJoinedAsync(result.GameId, result.JoinedPlayerId, result.JoinedPlayerName, result.TotalPlayers);
            }

            return result.ToApiResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining game");
            return ApiResponse<JoinGameResultDto>.ErrorResponse(ex.Message);
        }
    }

    public async Task<ApiResponse> AddAIPlayer(Guid gameId, AILevel level)
    {
        try
        {
            var playerId = GetPlayerId();
            var result = await _gameEngine.AddAIPlayerAsync(gameId, level, playerId);

            if (result.IsSuccess)
            {
                await NotifyPlayerJoinedAsync(gameId, result.PlayerId, result.PlayerName, result.TotalPlayers);
            }

            return result.ToApiResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding AI player");
            return ApiResponse.ErrorResponse(ex.Message);
        }
    }

    public async Task<ApiResponse> RemovePlayer(Guid gameId, Guid playerId)
    {
        try
        {
            var currentPlayerId = GetPlayerId();
            var result = await _gameEngine.RemovePlayerAsync(gameId, playerId, currentPlayerId);

            if (result.Success)
            {
                await NotifyPlayerLeftAsync(gameId, playerId,result.RemovedPlayerName ?? "");
            }

            return result.ToApiResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing player");
            return ApiResponse.ErrorResponse(ex.Message);
        }
    }

    public async Task<ApiResponse> StartGameAsync(Guid gameId)
    {
        try
        {
            var playerId = GetPlayerId();
            var game = await _gameEngine.StartGameAsync(gameId, playerId);

            if (game != null)
            {
                await NotifyGameStartedAsync(game.Id);

                // Send updated game state
                var gameStateDto = await _gameEngine.GetGameStateDtoAsync(game.Id);
                await NotifyGameStateUpdatedAsync(game.Id, gameStateDto);
            }

            return ApiResponse.SuccessResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting game");
            return ApiResponse.ErrorResponse(ex.Message);
        }
    }

    // ========== GAME ACTIONS ==========

    public async Task<ApiResponse<GameStateDto>> PlayCard(PlayCardRequest request)
    {
        try
        {
            var playerId = GetPlayerId();
            var result = await _gameEngine.PlayCardAsync(
                request.GameId,
                playerId,
                request.CardId,
                request.TargetPlayerId,
                request.TargetQueenId);

            if (result.Success)
            {
                var gameStateDto = await _gameEngine.GetGameStateDtoAsync(request.GameId);
                await NotifyGameStateUpdatedAsync(request.GameId, gameStateDto);
                return ApiResponse<GameStateDto>.SuccessResponse(gameStateDto);
            }

            return ApiResponse<GameStateDto>.ErrorResponse(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing card");
            return ApiResponse<GameStateDto>.ErrorResponse(ex.Message);
        }
    }

    public async Task<ApiResponse<GameStateDto>> DrawCard(Guid gameId)
    {
        try
        {
            var playerId = GetPlayerId();
            var result = await _gameEngine.DrawCardAsync(gameId, playerId);

            if (result.Success && result.UpdatedState != null)
            {
                await NotifyGameStateUpdatedAsync(gameId, result.UpdatedState);
                return ApiResponse<GameStateDto>.SuccessResponse(result.UpdatedState);
            }

            return ApiResponse<GameStateDto>.ErrorResponse(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing card");
            return new ApiResponse<GameStateDto>
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<ApiResponse<GameStateDto>> EndTurn(Guid gameId)
    {
        try
        {
            var playerId = GetPlayerId();
            var result = await _gameEngine.EndTurnAsync(gameId, playerId);

            if (result.Success && result.UpdatedState != null)
            {
                await NotifyGameStateUpdatedAsync(gameId, result.UpdatedState);
                return ApiResponse<GameStateDto>.SuccessResponse(result.UpdatedState);
            }

            return ApiResponse<GameStateDto>.ErrorResponse(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending turn");
            return new ApiResponse<GameStateDto>
            {
                Success = false,
                ErrorMessage = $"Error: {ex.Message}"
            };
        }
    }

    // ========== GAME STATE ==========

    public async Task<ApiResponse<GameStateDto>> GetGameState(Guid gameId)
    {
        try
        {
            var gameStateDto = await _gameEngine.GetGameStateDtoAsync(gameId);
            return ApiResponse<GameStateDto>.SuccessResponse(gameStateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting game state");
            return ApiResponse<GameStateDto>.ErrorResponse(ex.Message);
        }
    }

    public async Task<ActiveGamesResult> GetActiveGames()
    {
        try
        {
            return await _gameEngine.GetActiveGamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active games");
            return ActiveGamesResult.Error("Internal server error");
        }
    }

    // ========== CHAT ==========

    public async Task SendMessage(Guid gameId, string message)
    {
        var player = await _gameEngine.GetCurrentPlayerAsync(gameId);

        if (player != null)
        {
            var chatMessage = new ChatMessage
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await NotifyAllPlayersAsync(gameId, "ChatMessageReceived", chatMessage);
        }
    }

    // ========== PRIVATE HELPERS ==========

    private Guid GetPlayerId()
    {
        if (!_connectionPlayerMap.TryGetValue(Context.ConnectionId, out var playerId))
            throw new HubException("Player not authenticated");

        return playerId;
    }

    private async Task HandlePlayerDisconnect(Guid playerId)
    {
        try
        {
            // 1. Unmap connection (Hub responsibility)
            await UnmapPlayerConnectionAsync(playerId);

            // 2. Let GameEngine handle the business logic
            var result = await _gameEngine.HandlePlayerDisconnectAsync(playerId);

            // 3. Notify other players if needed
            if (result.ShouldNotifyPlayers && result.PlayerName != null)
            {
                await NotifyAllPlayersAsync(
                    result.GameId,
                    "PlayerLeft",
                    new PlayerLeftEvent
                    {
                        PlayerId = playerId,
                        PlayerName = result.PlayerName,
                        CanReconnect = result.CanReconnect,
                        Message = result.NotificationMessage
                    });
            }

            // 4. Log the disconnect
            if (result.IsGameActive)
            {
                _logger.LogPlayerDisconnectedWarning(playerId, result.CanReconnect);
            }
            else
            {
                _logger.LogPlayerDisconnectedWarning(playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogPlayerDisconnectError(ex, playerId);
        }
    }

    public async Task<ApiResponse> ReconnectPlayer(Guid gameId, Guid playerId)
    {
        try
        {
            var connectionId = Context.ConnectionId;

            // Check if within reconnection window (e.g., 2 minutes)
            if (_disconnectionTimes.TryGetValue(playerId, out var disconnectTime))
            {
                var timeSinceDisconnect = DateTime.UtcNow - disconnectTime;
                if (timeSinceDisconnect.TotalMinutes > 2)
                {
                    return ApiResponse.ErrorResponse("Reconnection window expired");
                }

                // Remove from disconnection tracking
                _disconnectionTimes.Remove(playerId);
            }

            var result = await _gameEngine.ReconnectPlayerAsync(gameId, playerId, connectionId);

            if (result.Success)
            {
                // Update connection mapping
                await MapPlayerConnectionAsync(playerId, connectionId);

                // Add to group
                await Groups.AddToGroupAsync(connectionId, gameId.ToString());

                // Notify players
                await NotifyAllPlayersAsync(gameId, "PlayerReconnected", new PlayerReconnectedEvent
                {
                    PlayerId = playerId,
                    PlayerName = result.PlayerName!
                });

                // Send current game state to reconnecting player
                var gameState = await _gameEngine.GetGameStateDtoAsync(gameId);
                await Clients.Client(connectionId).SendAsync("GameStateUpdated", gameState);
            }

            return result.ToApiResponse();
        }
        catch (Exception ex)
        {
            _logger.LogPlayerReconnectError(ex, playerId, gameId);
            return ApiResponse.ErrorResponse($"Reconnection failed: {ex.Message}");
        }
    }

    private async Task NotifyAllPlayersAsync<TEvent>(Guid gameId, string eventName, TEvent eventData)
    {
        try
        {
            await Clients.Group(gameId.ToString()).SendAsync(eventName, eventData);
        }
        catch (Exception ex)
        {
            _logger.LogFailedToNotifyPlayersWarning(ex, eventName, gameId);
        }
    }

    private async Task NotifySpecificPlayerAsync<TEvent>(Guid playerId, string eventName, TEvent eventData)
    {
        try
        {
            if (_playerConnectionMap.TryGetValue(playerId, out var connectionId))
            {
                await Clients.Client(connectionId).SendAsync(eventName, eventData);
            }
            else
            {
                _logger.LogCannotNotifyPlayerWarning(playerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogFailedToNotifyPlayerWarning(ex, playerId, eventName);
        }
    }

    // Convenience methods for common events
    private async Task NotifyPlayerJoinedAsync(Guid gameId, Guid playerId, string playerName, int totalPlayers)
    {
        await NotifyAllPlayersAsync(gameId, "PlayerJoined", new PlayerJoinedEvent
        {
            PlayerId = playerId,
            PlayerName = playerName,
            TotalPlayers = totalPlayers
        });
    }

    private async Task NotifyPlayerLeftAsync(Guid gameId, Guid playerId, string playerName)
    {
        await NotifyAllPlayersAsync(gameId, "PlayerLeft", new PlayerLeftEvent
        {
            PlayerId = playerId,
            PlayerName = playerName
        });
    }

    private async Task NotifyGameStateUpdatedAsync(Guid gameId, GameStateDto gameState)
    {
        await NotifyAllPlayersAsync(gameId, "GameStateUpdated", gameState);
    }

    private async Task NotifyGameStartedAsync(Guid gameId)
    {
        await NotifyAllPlayersAsync(gameId, "GameStarted", new GameStartedEvent
        {
            GameId = gameId,
            StartedAt = DateTime.UtcNow
        });
    }

    private static async Task MapPlayerConnectionAsync(Guid playerId, string connectionId)
    {
        // Remove old mapping if reconnecting
        if (_playerConnectionMap.TryGetValue(playerId, out var oldConnectionId))
        {
            _connectionPlayerMap.Remove(oldConnectionId);
        }

        _connectionPlayerMap[connectionId] = playerId;
        _playerConnectionMap[playerId] = connectionId;
    }

    private static async Task UnmapPlayerConnectionAsync(Guid playerId)
    {
        if (_playerConnectionMap.TryGetValue(playerId, out var connectionId))
        {
            _connectionPlayerMap.Remove(connectionId);
            _playerConnectionMap.Remove(playerId);

            // Also track disconnection time for reconnection window
            _disconnectionTimes[playerId] = DateTime.UtcNow;
        }
    }
}