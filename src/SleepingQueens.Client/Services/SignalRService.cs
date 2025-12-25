using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using SleepingQueens.Client.Events;
using SleepingQueens.Client.Logging;
using SleepingQueens.Shared.Models.DTOs;
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

public class SignalRService(
    NavigationManager navigationManager,
    IConfiguration configuration,
    ILogger<SignalRService> logger) : ISignalRService
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigationManager = navigationManager;
    private readonly ILogger<SignalRService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;
    private bool _isDisposed;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // Async Events
    public IAsyncEvent<GameStateDto> OnGameStateUpdated { get; } = new AsyncEvent<GameStateDto>(logger);
    public IAsyncEvent<GameStartedEvent> OnGameStarted { get; } = new AsyncEvent<GameStartedEvent>(logger);
    public IAsyncEvent<PlayerJoinedEvent> OnPlayerJoined { get; } = new AsyncEvent<PlayerJoinedEvent>(logger);
    public IAsyncEvent<PlayerLeftEvent> OnPlayerLeft { get; } = new AsyncEvent<PlayerLeftEvent>(logger);
    public IAsyncEvent<(string PlayerName, string Message)> OnChatMessage { get; } = new AsyncEvent<(string, string)>(logger);
    public IAsyncEvent<Exception> OnConnectionError { get; } = new AsyncEvent<Exception>(logger);
    public IAsyncEvent<string> OnConnectionStatusChanged { get; } = new AsyncEvent<string>(logger);
    public IAsyncEvent<ApiResponse> OnGameActionResponse { get; } = new AsyncEvent<ApiResponse>(logger);

    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;
    public bool IsConnected => ConnectionState == HubConnectionState.Connected;

    public async Task ConnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection != null && _hubConnection.State != HubConnectionState.Disconnected)
            {
                _logger.LogAlreadyConnected(_hubConnection.State);
                return;
            }

            _logger.LogInformation("Connecting to SignalR hub...");

            // Dispose existing connection if any
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
            }

            // Get base URL from config or fallback to navigation manager
            var baseUrl = _configuration["ApiBaseUrl"] ?? _navigationManager.BaseUri;

            // Build new connection
            _hubConnection = new HubConnectionBuilder()
                .WithUrl($"{baseUrl}gamehub") // This will now be correct
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
                _logger.LogSignalRConnected(_hubConnection.ConnectionId);
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
            _logger.LogReceivedGameStartedEvent(gameStartedEvent.GameId);
            await OnGameStarted.InvokeAsync(gameStartedEvent);
        });

        _hubConnection.On<PlayerJoinedEvent>("PlayerJoined", async (playerJoinedEvent) =>
        {
            _logger.LogReceivedPlayerJoinedEvent(playerJoinedEvent.PlayerName);
            await OnPlayerJoined.InvokeAsync(playerJoinedEvent);
        });

        _hubConnection.On<PlayerLeftEvent>("PlayerLeft", async (playerLeftEvent) =>
        {
            _logger.LogReceivedPlayerLeftEvent(playerLeftEvent.PlayerName);
            await OnPlayerLeft.InvokeAsync(playerLeftEvent);
        });

        _hubConnection.On<ChatMessage>("ChatMessageReceived", async (chatMessage) =>
        {
            _logger.LogReceivedChatMessageEvent(chatMessage.PlayerName);
            await OnChatMessage.InvokeAsync((chatMessage.PlayerName, chatMessage.Message));
        });

        _hubConnection.Closed += async (error) =>
        {
            var status = error != null ? $"Closed with error: {error.Message}" : "Closed";
            _logger.LogSignalRConnectionClosedWarning(status);

            if (error != null)
            {
                await OnConnectionError.InvokeAsync(error);
            }
            await OnConnectionStatusChanged.InvokeAsync(status);
        };

        _hubConnection.Reconnected += (connectionId) =>
        {
            _logger.LogSignalRReconnected(connectionId ?? "");
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

            _logger.LogGameCreated(request.PlayerName);

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

            _logger.LogPlayerJoiningGame(request.PlayerName, request.GameCode);

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

            _logger.LogGameStarted(gameId);

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

            _logger.LogPlayingCard(request.CardId, request.GameId);

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

            _logger.LogDrawCard(gameId);

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

            _logger.LogEndingTurn(gameId);

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

            int cardCount = cardIds.Count();
            _logger.LogDiscardingCards(cardCount, gameId);

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

            _logger.LogGettingGameState(gameId);

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

            _logger.LogSendingChatMessage(gameId);

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

            _logger.LogAddingAIPlayer(level, gameId);

            return await _hubConnection.InvokeAsync<ApiResponse>("AddAIPlayer", gameId, level);
        }, "AddAIPlayer");
    }

    public async Task<ApiResponse> RemovePlayerAsync(Guid gameId, Guid playerId)
    {
        return await ExecuteHubMethodAsync<ApiResponse>(async () =>
        {
            if (_hubConnection == null)
                throw new HubException("Not connected to SignalR hub");

            _logger.LogRemovingPlayer(playerId, gameId);

            return await _hubConnection.InvokeAsync<ApiResponse>("RemovePlayer", gameId, playerId);
        }, "RemovePlayer");
    }

    #endregion

    #region Private Helper Methods

    private async Task<T> ExecuteHubMethodAsync<T>(Func<Task<T>> operation, string methodName)
    {
        try
        {
            _logger.LogExecutingSignalIRMethod(methodName);

            if (!IsConnected)
            {
                _logger.LogAttemptingToExecuteSignalIRMethodWarning(methodName);
                await ConnectAsync();
            }

            var result = await operation();
            _logger.LogSignalRMethodExecutedSuccessfully(methodName);
            return result;
        }
        catch (HubException hubEx)
        {
            _logger.LogHubError(hubEx, methodName);
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
            _logger.LogExecutingSignalRMethodError(ex, methodName);
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
public class SignalRRetryPolicy(ILogger logger) : IRetryPolicy
{
    private readonly ILogger _logger = logger;
    private static readonly TimeSpan[] _retryDelays =
    [
        TimeSpan.Zero,           // Immediate retry for first failure
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1)
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        if (retryContext.PreviousRetryCount >= _retryDelays.Length - 1)
        {
            _logger.LogMaxRetriesWarning(retryContext.PreviousRetryCount);
            return null; // Stop retrying
        }

        var delay = _retryDelays[retryContext.PreviousRetryCount];

        _logger.LogRetryAttemptWarning(retryContext.PreviousRetryCount + 1,
            retryContext.ElapsedTime.TotalMilliseconds,
            delay);

        return delay;
    }
}
#endregion