namespace SleepingQueens.Client.Logging;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SleepingQueens.Shared.Models.Game.Enums;

public static partial class LoggerExtensions
{
    // Error Logging

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Error,
        Message = "Hub exception in SignalR method {MethodName}")]
    public static partial void LogHubError(this ILogger logger, Exception ex, string methodName);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Error,
        Message = "Error executing SignalR method {MethodName}")]
    public static partial void LogExecutingSignalRMethodError(this ILogger logger, Exception ex, string methodName);

    // Warning Logging

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "SignalR connection closed: {Status}")]
    public static partial void LogSignalRConnectionClosedWarning(this ILogger logger, string status);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message = "Attempting to execute {MethodName} while disconnected. Reconnecting...")]
    public static partial void LogAttemptingToExecuteSignalIRMethodWarning(this ILogger logger, string methodName);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Warning,
        Message = "Max retry attempts reached ({Attempts}). Stopping retries.")]
    public static partial void LogMaxRetriesWarning(this ILogger logger, long attempts);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Warning,
        Message = "SignalR retry attempt {Attempt} after {ElapsedMilliseconds}ms. Next delay: {Delay}")]
    public static partial void LogRetryAttemptWarning(this ILogger logger, long attempt, double elapsedMilliseconds, TimeSpan delay);

    // Debug Logging

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Already connected or connecting. State: {State}")]
    public static partial void LogAlreadyConnected(this ILogger logger, HubConnectionState state);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "Received GameStarted event. GameId: {GameId}")]
    public static partial void LogReceivedGameStartedEvent(this ILogger logger, Guid gameId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "Received PlayerJoined event. Player: {PlayerName}")]
    public static partial void LogReceivedPlayerJoinedEvent(this ILogger logger, string playerName);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Debug,
        Message = "Received chat message from {PlayerName}")]
    public static partial void LogReceivedChatMessageEvent(this ILogger logger, string playerName);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Debug,
        Message = "Received PlayerLeft event. Player: {PlayerName}")]
    public static partial void LogReceivedPlayerLeftEvent(this ILogger logger, string playerName);

    [LoggerMessage(
        EventId = 3006,
        Level = LogLevel.Debug,
        Message = "Drawing card in game {GameId}")]
    public static partial void LogDrawCard(this ILogger logger, Guid gameId);

    [LoggerMessage(
        EventId = 3007,
        Level = LogLevel.Debug,
        Message = "Ending turn in game {GameId}")]
    public static partial void LogEndingTurn(this ILogger logger, Guid gameId);

    [LoggerMessage(
        EventId = 3008,
        Level = LogLevel.Debug,
        Message = "Discarding {Count} cards in game {GameId}")]
    public static partial void LogDiscardingCards(this ILogger logger, int count, Guid gameId);

    [LoggerMessage(
        EventId = 3009,
        Level = LogLevel.Debug,
        Message = "Getting game state for game {GameId}")]
    public static partial void LogGettingGameState(this ILogger logger, Guid gameId);

    [LoggerMessage(
        EventId = 3010,
        Level = LogLevel.Debug,
        Message = "Sending chat message to game {GameId}")]
    public static partial void LogSendingChatMessage(this ILogger logger, Guid gameId);

    [LoggerMessage(
        EventId = 3011,
        Level = LogLevel.Debug,
        Message = "Executing SignalR method: {MethodName}")]
    public static partial void LogExecutingSignalIRMethod(this ILogger logger, string methodName);

    [LoggerMessage(
        EventId = 3012,
        Level = LogLevel.Debug,
        Message = "SignalR method {MethodName} executed successfully")]
    public static partial void LogSignalRMethodExecutedSuccessfully(this ILogger logger, string methodName);

    // Informational Logging

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "SignalR connected successfully. Connection ID: {ConnectionId}")]
    public static partial void LogSignalRConnected(this ILogger logger, string? connectionId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "SignalR reconnected. New Connection ID: {ConnectionId}")]
    public static partial void LogSignalRReconnected(this ILogger logger, string connectionId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Creating game for player: {PlayerName}")]
    public static partial void LogGameCreated(this ILogger logger, string playerName);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Player {PlayerName} joining game: {GameCode}")]
    public static partial void LogPlayerJoiningGame(this ILogger logger, string playerName, string gameCode);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Starting game: {GameId}")]
    public static partial void LogGameStarted(this ILogger logger, Guid gameId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Playing card {CardId} in game {GameId}")]
    public static partial void LogPlayingCard(this ILogger logger, Guid cardId, Guid gameId);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "Adding AI player (Level: {Level}) to game {GameId}")]
    public static partial void LogAddingAIPlayer(this ILogger logger, AILevel level, Guid gameId);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Information,
        Message = "Removing player {PlayerId} from game {GameId}")]
    public static partial void LogRemovingPlayer(this ILogger logger, Guid playerId, Guid gameId);

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Information,
        Message = "Client disconnected: {ConnectionId}")]
    public static partial void LogClientDisconnected(this ILogger logger, string connectionId);

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Information,
        Message = "Creating game for player: {PlayerName}")]
    public static partial void LogGameCreatedForPlayer(this ILogger logger, string playerName);

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Player {PlayerName} joining game {GameCode}")]
    public static partial void LogPlayerJoinedGame(this ILogger logger, string playerName, string gameCode);

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Client connected: {ConnectionId}")]
    public static partial void LogClientConnected(this ILogger logger, string connectionId);
}
