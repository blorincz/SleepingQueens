using Microsoft.AspNetCore.SignalR;
using SleepingQueens.Server.Data.Repositories;
using SleepingQueens.Server.GameEngine;        // IGameEngine from Shared
using SleepingQueens.Shared.Models.DTOs;       // DTOs from Shared
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;       // Domain models from Shared

namespace SleepingQueens.Server.Hubs;

public class GameHub(
    IGameEngine gameEngine,
    IGameRepository gameRepository,
    ILogger<GameHub> logger) : Hub
{
    private readonly IGameEngine _gameEngine = gameEngine;
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ILogger<GameHub> _logger = logger;
    private static readonly Dictionary<string, Guid> _connectionPlayerMap = [];
    private static readonly Dictionary<Guid, string> _playerConnectionMap = [];

    // ========== CONNECTION MANAGEMENT ==========

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Handle player disconnect
        if (_connectionPlayerMap.TryGetValue(Context.ConnectionId, out var playerId))
        {
            await HandlePlayerDisconnect(playerId);
        }

        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ========== GAME MANAGEMENT ==========

    public async Task<ApiResponse<CreateGameResult>> CreateGame(CreateGameRequest request)
    {
        try
        {
            _logger.LogInformation("Creating game for player: {PlayerName}", request.PlayerName);

            var creator = new Player
            {
                Name = request.PlayerName,
                ConnectionId = Context.ConnectionId
            };

            var game = _gameEngine.CreateGame(request.Settings, creator);

            // Initialize the game in repository
            await _gameRepository.InitializeNewGameAsync(game, creator);

            // Map connection to player
            _connectionPlayerMap[Context.ConnectionId] = creator.Id;
            _playerConnectionMap[creator.Id] = Context.ConnectionId;

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

    public async Task<ApiResponse<JoinGameResult>> JoinGame(JoinGameRequest request)
    {
        var playerName = request.PlayerName;
        var gameCode = request.GameCode;

        try
        {
            _logger.LogInformation("Player {PlayerName} joining game {GameCode}", playerName, gameCode);

            var game = await _gameRepository.GetByCodeAsync(gameCode);
            if (game == null)
                return new ApiResponse<JoinGameResult>() { Success = false, ErrorMessage = "Game not found" };

            if (game.IsFull())
                return new ApiResponse<JoinGameResult>() { Success = false, ErrorMessage = "Game is full" };

            if (game.Status != GameStatus.Waiting)
                return new ApiResponse<JoinGameResult>() { Success = false, ErrorMessage = "Game already started" };

            var player = new Player
            {
                Name = playerName,
                ConnectionId = Context.ConnectionId,
                GameId = game.Id
            };

            var addedPlayer = await _gameRepository.AddPlayerAsync(game.Id, player);

            // Map connection to player
            _connectionPlayerMap[Context.ConnectionId] = addedPlayer.Id;
            _playerConnectionMap[addedPlayer.Id] = Context.ConnectionId;

            // Add to game group
            await Groups.AddToGroupAsync(Context.ConnectionId, game.Id.ToString());

            // Notify other players
            await Clients.Group(game.Id.ToString())
                .SendAsync("PlayerJoined", new PlayerJoinedEvent
                {
                    PlayerId = addedPlayer.Id,
                    PlayerName = playerName,
                    TotalPlayers = game.Players.Count
                });

            // Send initial game state to joining player
            var gameStateDto = await _gameEngine.GetGameStateDtoAsync(game.Id);

            return ApiResponse<JoinGameResult>.SuccessResponse(new JoinGameResult
            {
                GameId = game.Id,
                PlayerId = addedPlayer.Id,
                GameState = gameStateDto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining game");
            return ApiResponse<JoinGameResult>.ErrorResponse(ex.Message);
        }
    }

    public async Task<StartGameResponse> StartGame(Guid gameId)
    {
        try
        {
            var playerId = GetPlayerId();
            var game = await _gameRepository.GetByIdAsync(gameId);

            if (game == null)
                return new StartGameResponse { Success = false, ErrorMessage = "Game not found" };

            // Check if player is in this game
            if (!game.Players.Any(p => p.Id == playerId))
                return new StartGameResponse { Success = false, ErrorMessage = "Not in game" };

            // Start the game
            var startedGame = await _gameEngine.StartGameAsync(gameId);

            // Notify all players
            await Clients.Group(gameId.ToString())
                .SendAsync("GameStarted", new GameStartedEvent
                {
                    GameId = gameId,
                    StartedAt = startedGame.StartedAt!.Value,
                    Players = [.. startedGame.Players.Select(p => new PlayerInfo
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsCurrentTurn = p.IsCurrentTurn
                    })]
                });

            // Send initial game state to all players
            var gameState = await _gameEngine.GetGameStateDtoAsync(gameId);
            await Clients.Group(gameId.ToString())
                .SendAsync("GameStateUpdated", gameState);

            return new StartGameResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting game");
            return new StartGameResponse { Success = false, ErrorMessage = ex.Message };
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

                // Notify all players
                await Clients.Group(request.GameId.ToString())
                    .SendAsync("GameStateUpdated", gameStateDto);

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

    public async Task<DrawCardResponse> DrawCard(Guid gameId)
    {
        try
        {
            var playerId = GetPlayerId();

            var result = await _gameEngine.DrawCardAsync(gameId, playerId);

            if (result.Success && result.UpdatedState != null)
            {
                await Clients.Group(gameId.ToString())
                    .SendAsync("GameStateUpdated", result.UpdatedState);
            }

            return new DrawCardResponse
            {
                Success = result.Success,
                Message = result.Message,
                GameState = result.UpdatedState
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing card");
            return new DrawCardResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<EndTurnResponse> EndTurn(Guid gameId)
    {
        try
        {
            var playerId = GetPlayerId();

            var result = await _gameEngine.EndTurnAsync(gameId, playerId);

            if (result.Success && result.UpdatedState != null)
            {
                await Clients.Group(gameId.ToString())
                    .SendAsync("GameStateUpdated", result.UpdatedState);
            }

            return new EndTurnResponse
            {
                Success = result.Success,
                Message = result.Message,
                GameState = result.UpdatedState
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending turn");
            return new EndTurnResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
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

    public async Task<IEnumerable<ActiveGameInfo>> GetActiveGames()
    {
        var games = await _gameRepository.GetActiveGamesAsync();
        return games.Select(g => new ActiveGameInfo
        {
            GameId = g.Id,
            GameCode = g.Code,
            PlayerCount = g.Players.Count,
            MaxPlayers = g.MaxPlayers,
            Status = g.Status,
            CreatedAt = g.CreatedAt
        });
    }

    // ========== CHAT ==========

    public async Task SendMessage(Guid gameId, string message)
    {
        var playerId = GetPlayerId();
        var player = await _gameRepository.GetPlayerAsync(playerId);

        if (player != null)
        {
            var chatMessage = new ChatMessage
            {
                PlayerId = playerId,
                PlayerName = player.Name,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await Clients.Group(gameId.ToString())
                .SendAsync("ChatMessageReceived", chatMessage);
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
        _connectionPlayerMap.Remove(Context.ConnectionId);
        _playerConnectionMap.Remove(playerId);

        var player = await _gameRepository.GetPlayerAsync(playerId);
        if (player == null) return;

        var gameId = player.GameId;

        // Notify other players
        await Clients.Group(gameId.ToString())
            .SendAsync("PlayerLeft", new PlayerLeftEvent
            {
                PlayerId = playerId,
                PlayerName = player.Name
            });

        // If game hasn't started yet, remove the player
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game?.Status == GameStatus.Waiting)
        {
            await _gameRepository.RemovePlayerAsync(playerId);
        }
        // If game is active, mark player as disconnected
        else if (game?.Status == GameStatus.Active)
        {
            // Could implement reconnection logic here
            _logger.LogWarning("Player {PlayerId} disconnected during active game", playerId);
        }
    }
}