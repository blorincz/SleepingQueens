using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SleepingQueens.Client.Events;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using System.Text.Json;

namespace SleepingQueens.Client.Services;

public interface ISignalRService : IAsyncDisposable
{
    // Async Events
    IAsyncEvent<GameStateDto> OnGameStateUpdated { get; }
    IAsyncEvent<GameStartedEvent> OnGameStarted { get; }
    IAsyncEvent<PlayerJoinedEvent> OnPlayerJoined { get; }
    IAsyncEvent<PlayerLeftEvent> OnPlayerLeft { get; }
    IAsyncEvent<(string PlayerName, string Message)> OnChatMessage { get; }
    IAsyncEvent<Exception> OnConnectionError { get; }
    IAsyncEvent<string> OnConnectionStatusChanged { get; }
    IAsyncEvent<ApiResponse> OnGameActionResponse { get; }

    // Connection Management
    Task ConnectAsync();
    Task DisconnectAsync();
    HubConnectionState ConnectionState { get; }
    bool IsConnected { get; }

    // Game Management
    Task<ApiResponse<CreateGameResult>> CreateGameAsync(CreateGameRequest request);
    Task<ApiResponse<JoinGameResult>> JoinGameAsync(JoinGameRequest request);
    Task<ApiResponse> StartGameAsync(Guid gameId);

    // Game Actions
    Task<ApiResponse<GameStateDto>> PlayCardAsync(PlayCardRequest request);
    Task<ApiResponse<DrawCardResult>> DrawCardAsync(Guid gameId);
    Task<ApiResponse<GameStateDto>> EndTurnAsync(Guid gameId);
    Task<ApiResponse<GameStateDto>> DiscardCardsAsync(Guid gameId, IEnumerable<Guid> cardIds);

    // Game State
    Task<ApiResponse<GameStateDto>> GetGameStateAsync(Guid gameId);
    Task<ApiResponse<IEnumerable<ActiveGameInfo>>> GetActiveGamesAsync();

    // Chat
    Task<ApiResponse> SendMessageAsync(Guid gameId, string message);

    // Utility
    Task<ApiResponse> AddAIPlayerAsync(Guid gameId, AILevel level);
    Task<ApiResponse> RemovePlayerAsync(Guid gameId, Guid playerId);
}

public class SignalRService : ISignalRService
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<SignalRService> _logger;
    private bool _isDisposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // Async Events
    public IAsyncEvent<GameStateDto> OnGameStateUpdated { get; }
    public IAsyncEvent<GameStartedEvent> OnGameStarted { get; }
    public IAsyncEvent<PlayerJoinedEvent> OnPlayerJoined { get; }
    public IAsyncEvent<PlayerLeftEvent> OnPlayerLeft { get; }
    public IAsyncEvent<(string PlayerName, string Message)> OnChatMessage { get; }
    public IAsyncEvent<Exception> OnConnectionError { get; }
    public IAsyncEvent<string> OnConnectionStatusChanged { get; }
    public IAsyncEvent<ApiResponse> OnGameActionResponse { get; }

    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => ConnectionState == HubConnectionState.Connected;

    public SignalRService(
        NavigationManager navigationManager,
        ILogger<SignalRService> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;

        // Initialize async events with logging
        OnGameStateUpdated = new AsyncEvent<GameStateDto>(logger);
        OnGameStarted = new AsyncEvent<GameStartedEvent>(logger);
        OnPlayerJoined = new AsyncEvent<PlayerJoinedEvent>(logger);
        OnPlayerLeft = new AsyncEvent<PlayerLeftEvent>(logger);
        OnChatMessage = new AsyncEvent<(string, string)>(logger);
        OnConnectionError = new AsyncEvent<Exception>(logger);
        OnConnectionStatusChanged = new AsyncEvent<string>(logger);
        OnGameActionResponse = new AsyncEvent<ApiResponse>(logger);
    }

    public async Task ConnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected)
            {
                _logger.LogDebug("Already connected or connecting. State: {State}", _hubConnection.State);
                return;
            }

            _logger.LogInformation("Connecting to SignalR hub...");

            // Dispose existing connection if any
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            // Build new connection
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{_navigationManager.BaseUri}gamehub") // This will now be correct
                .WithAutomaticReconnect(new SignalRRetryPolicy(_logger))
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                })
                .Build();

            SetupHubListeners();

            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("SignalR connected successfully. Connection ID: {ConnectionId}", _hubConnection.ConnectionId);
                await OnConnectionStatusChanged.InvokeAsync("Connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to SignalR hub");
                await OnConnectionError.InvokeAsync(ex);
                await OnConnectionStatusChanged.InvokeAsync($"Connection failed: {ex.Message}");
                throw;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection == null) return;

            _logger.LogInformation("Disconnecting from SignalR hub...");

            try
            {
                await _hubConnection.StopAsync();
                await OnConnectionStatusChanged.InvokeAsync("Disconnected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SignalR disconnection");
                await OnConnectionError.InvokeAsync(ex);
            }
            finally
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void SetupHubListeners()
    {
        if (_hubConnection == null) return;

        _hubConnection.On<GameStateDto>("GameStateUpdated", async (gameState) =>
        {
            _logger.LogDebug("Received GameStateUpdated event");
            await OnGameStateUpdated.InvokeAsync(gameState);
        });

        _hubConnection.On<GameStartedEvent>("GameStarted", async (gameStartedEvent) =>
        {
            _logger.LogDebug("Received GameStarted event. GameId: {GameId}", gameStartedEvent.GameId);
            await OnGameStarted.InvokeAsync(gameStartedEvent);
        });

        _hubConnection.On<PlayerJoinedEvent>("PlayerJoined", async (playerJoinedEvent) =>
        {
            _logger.LogDebug("Received PlayerJoined event. Player: {PlayerName}", playerJoinedEvent.PlayerName);
            await OnPlayerJoined.InvokeAsync(playerJoinedEvent);
        });

        _hubConnection.On<PlayerLeftEvent>("PlayerLeft", async (playerLeftEvent) =>
        {
            _logger.LogDebug("Received PlayerLeft event. Player: {PlayerName}", playerLeftEvent.PlayerName);
            await OnPlayerLeft.InvokeAsync(playerLeftEvent);
        });

        _hubConnection.On<ChatMessage>("ChatMessageReceived", async (chatMessage) =>
        {
            _logger.LogDebug("Received chat message from {PlayerName}", chatMessage.PlayerName);
            await OnChatMessage.InvokeAsync((chatMessage.PlayerName, chatMessage.Message));
        });

        _hubConnection.Closed += async (error) =>
        {
            var status = error != null ? $"Closed with error: {error.Message}" : "Closed";
            _logger.LogWarning("SignalR connection closed: {Status}", status);

            if (error != null)
            {
                await OnConnectionError.InvokeAsync(error);
            }
            await OnConnectionStatusChanged.InvokeAsync(status);
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogInformation("SignalR reconnected. New Connection ID: {ConnectionId}", connectionId);
            return OnConnectionStatusChanged.InvokeAsync("Reconnected");
        };

        _hubConnection.Reconnecting += (error) =>
        {
            _logger.LogWarning(error, "SignalR reconnecting...");
            return OnConnectionStatusChanged.InvokeAsync("Reconnecting");
        };
    }

    #region Game Management

    public async Task<ApiResponse<CreateGameResult>> CreateGameAsync(CreateGameRequest request)
    {
        return await ExecuteHubMethodAsync<ApiResponse<CreateGameResult>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogInformation("Creating game for player: {PlayerName}", request.PlayerName);

            var response = await _hubConnection.InvokeAsync<ApiResponse<CreateGameResult>>("CreateGame", request);
            await NotifyGameActionResponse(response);
            return response;
        }, "CreateGame");
    }

    public async Task<ApiResponse<JoinGameResult>> JoinGameAsync(JoinGameRequest request)
    {
        return await ExecuteHubMethodAsync<ApiResponse<JoinGameResult>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogInformation("Player {PlayerName} joining game: {GameCode}",
                request.PlayerName, request.GameCode);

            var response = await _hubConnection.InvokeAsync<ApiResponse<JoinGameResult>>("JoinGame", request);
            await NotifyGameActionResponse(response);
            return response;
        }, "JoinGame");
    }

    public async Task<ApiResponse> StartGameAsync(Guid gameId)
    {
        return await ExecuteHubMethodAsync<ApiResponse>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogInformation("Starting game: {GameId}", gameId);

            // Map from StartGameResponse to ApiResponse
            var response = await _hubConnection.InvokeAsync<StartGameResponse>("StartGame", gameId);
            var apiResponse = new ApiResponse
            {
                Success = response.Success,
                ErrorMessage = response.ErrorMessage
            };

            await NotifyGameActionResponse(apiResponse);
            return apiResponse;
        }, "StartGame");
    }

    #endregion

    #region Game Actions

    public async Task<ApiResponse<GameStateDto>> PlayCardAsync(PlayCardRequest request)
    {
        return await ExecuteHubMethodAsync<ApiResponse<GameStateDto>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Playing card {CardId} in game {GameId}", request.CardId, request.GameId);

            var response = await _hubConnection.InvokeAsync<ApiResponse<GameStateDto>>("PlayCard", request);
            await NotifyGameActionResponse(response);
            return response;
        }, "PlayCard");
    }

    public async Task<ApiResponse<DrawCardResult>> DrawCardAsync(Guid gameId)
    {
        return await ExecuteHubMethodAsync<ApiResponse<DrawCardResult>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Drawing card in game {GameId}", gameId);

            // Map from DrawCardResponse to ApiResponse<DrawCardResult>
            var response = await _hubConnection.InvokeAsync<DrawCardResponse>("DrawCard", gameId);

            var apiResponse = new ApiResponse<DrawCardResult>
            {
                Success = response.Success,
                ErrorMessage = response.Message,
                Data = response.Success ? new DrawCardResult
                {
                    CardId = response.CardId ?? Guid.Empty,
                    CardName = response.CardName ?? "Unknown Card",
                    GameState = response.GameState
                } : null
            };

            await NotifyGameActionResponse(apiResponse);
            return apiResponse;
        }, "DrawCard");
    }

    public async Task<ApiResponse<GameStateDto>> EndTurnAsync(Guid gameId)
    {
        return await ExecuteHubMethodAsync<ApiResponse<GameStateDto>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Ending turn in game {GameId}", gameId);

            // Map from EndTurnResponse to ApiResponse<GameStateDto>
            var response = await _hubConnection.InvokeAsync<EndTurnResponse>("EndTurn", gameId);

            var apiResponse = new ApiResponse<GameStateDto>
            {
                Success = response.Success,
                ErrorMessage = response.Message,
                Data = response.GameState
            };

            await NotifyGameActionResponse(apiResponse);
            return apiResponse;
        }, "EndTurn");
    }

    public async Task<ApiResponse<GameStateDto>> DiscardCardsAsync(Guid gameId, IEnumerable<Guid> cardIds)
    {
        return await ExecuteHubMethodAsync<ApiResponse<GameStateDto>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Discarding {Count} cards in game {GameId}", cardIds.Count(), gameId);

            // Note: You need to add a DiscardCards method to your GameHub
            // For now, returning a placeholder response
            throw new NotImplementedException("DiscardCards method not implemented in GameHub");
        }, "DiscardCards");
    }

    #endregion

    #region Game State

    public async Task<ApiResponse<GameStateDto>> GetGameStateAsync(Guid gameId)
    {
        return await ExecuteHubMethodAsync<ApiResponse<GameStateDto>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Getting game state for game {GameId}", gameId);

            var response = await _hubConnection.InvokeAsync<ApiResponse<GameStateDto>>("GetGameState", gameId);
            return response;
        }, "GetGameState");
    }

    public async Task<ApiResponse<IEnumerable<ActiveGameInfo>>> GetActiveGamesAsync()
    {
        return await ExecuteHubMethodAsync<ApiResponse<IEnumerable<ActiveGameInfo>>>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Getting active games list");

            var games = await _hubConnection.InvokeAsync<IEnumerable<ActiveGameInfo>>("GetActiveGames");

            return new ApiResponse<IEnumerable<ActiveGameInfo>>
            {
                Success = true,
                Data = games
            };
        }, "GetActiveGames");
    }

    #endregion

    #region Chat

    public async Task<ApiResponse> SendMessageAsync(Guid gameId, string message)
    {
        return await ExecuteHubMethodAsync<ApiResponse>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogDebug("Sending chat message to game {GameId}", gameId);

            await _hubConnection.SendAsync("SendMessage", gameId, message);

            return new ApiResponse
            {
                Success = true
            };
        }, "SendMessage");
    }

    #endregion

    #region Utility Methods

    public async Task<ApiResponse> AddAIPlayerAsync(Guid gameId, AILevel level)
    {
        return await ExecuteHubMethodAsync<ApiResponse>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogInformation("Adding AI player (Level: {Level}) to game {GameId}", level, gameId);

            return await _hubConnection.InvokeAsync<ApiResponse>("AddAIPlayer", gameId, level);
        }, "AddAIPlayer");
    }

    public async Task<ApiResponse> RemovePlayerAsync(Guid gameId, Guid playerId)
    {
        return await ExecuteHubMethodAsync<ApiResponse>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogInformation("Removing player {PlayerId} from game {GameId}", playerId, gameId);

            return await _hubConnection.InvokeAsync<ApiResponse>("RemovePlayer", gameId, playerId);
        }, "RemovePlayer");
    }

    #endregion

    #region Private Helper Methods

    private async Task<T> ExecuteHubMethodAsync<T>(Func<Task<T>> operation, string methodName)
    {
        try
        {
            _logger.LogDebug("Executing SignalR method: {MethodName}", methodName);

            if (!IsConnected)
            {
                _logger.LogWarning("Attempting to execute {MethodName} while disconnected. Reconnecting...", methodName);
                await ConnectAsync();
            }

            var result = await operation();
            _logger.LogDebug("SignalR method {MethodName} executed successfully", methodName);
            return result;
        }
        catch (HubException hubEx)
        {
            _logger.LogError(hubEx, "Hub exception in SignalR method {MethodName}", methodName);
            await OnConnectionError.InvokeAsync(hubEx);

            // For generic methods, we need to handle the return type properly
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ApiResponse<>))
            {
                // Create error response for generic ApiResponse<T>
                var errorType = typeof(T).GetGenericArguments()[0];
                var errorResponseType = typeof(ApiResponse<>).MakeGenericType(errorType);
                var errorResponse = Activator.CreateInstance(errorResponseType);

                var successProp = errorResponseType.GetProperty("Success");
                var errorMessageProp = errorResponseType.GetProperty("ErrorMessage");

                successProp?.SetValue(errorResponse, false);
                errorMessageProp?.SetValue(errorResponse, $"Hub error in {methodName}: {hubEx.Message}");

                return (T)errorResponse!;
            }
            else if (typeof(T) == typeof(ApiResponse))
            {
                // For non-generic ApiResponse
                return (T)(object)ApiResponse.ErrorResponse($"Hub error in {methodName}: {hubEx.Message}");
            }

            throw new HubException($"Hub error in {methodName}: {hubEx.Message}", hubEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SignalR method {MethodName}", methodName);
            await OnConnectionError.InvokeAsync(ex);
            throw;
        }
    }

    private async Task NotifyGameActionResponse<T>(ApiResponse<T> response)
    {
        var genericResponse = new ApiResponse
        {
            Success = response.Success,
            ErrorMessage = response.ErrorMessage
        };

        await OnGameActionResponse.InvokeAsync(genericResponse);
    }

    private async Task NotifyGameActionResponse(ApiResponse response)
    {
        await OnGameActionResponse.InvokeAsync(response);
    }

    #endregion

    #region Cleanup

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _logger.LogInformation("Disposing SignalRService...");

        try
        {
            await DisconnectAsync();
            _connectionLock?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SignalRService disposal");
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    #endregion
}

#region Supporting Classes

// Custom retry policy with exponential backoff
public class SignalRRetryPolicy : IRetryPolicy
{
    private readonly ILogger _logger;
    private static readonly TimeSpan[] _retryDelays = new[]
    {
        TimeSpan.Zero,           // Immediate retry for first failure
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1)
    };

    public SignalRRetryPolicy(ILogger logger)
    {
        _logger = logger;
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.PreviousRetryCount >= _retryDelays.Length - 1)
        {
            _logger.LogWarning("Max retry attempts reached ({Attempts}). Stopping retries.",
                retryContext.PreviousRetryCount);
            return null; // Stop retrying
        }

        var delay = _retryDelays[retryContext.PreviousRetryCount];

        _logger.LogWarning(
            "SignalR retry attempt {Attempt} after {ElapsedMilliseconds}ms. Next delay: {Delay}",
            retryContext.PreviousRetryCount + 1,
            retryContext.ElapsedTime.TotalMilliseconds,
            delay);

        return delay;
    }
}
#endregion