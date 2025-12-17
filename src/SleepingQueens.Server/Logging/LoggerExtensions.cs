namespace SleepingQueens.Server.Logging;

using Microsoft.Extensions.Logging;

public static partial class LoggerExtensions
{
    // Error Logging

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Error,
        Message = "Error getting game {GameId}")]
    public static partial void LogGameFetchError(this ILogger logger, Exception ex, Guid gameId);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Error,
        Message = "Error starting game {GameId}")]
    public static partial void LogGameStartError(this ILogger logger, Exception ex, Guid gameId);

    [LoggerMessage(
        EventId = 9003,
        Level = LogLevel.Error,
        Message = "Error getting players for game {GameId}")]
    public static partial void LogGamePlayerFetchError(this ILogger logger, Exception ex, Guid gameId);

    [LoggerMessage(
        EventId = 9004,
        Level = LogLevel.Error,
        Message = "Error getting player {PlayerId}")]
    public static partial void LogPlayerFetchError(this ILogger logger, Exception ex, Guid playerId);

    [LoggerMessage(
        EventId = 9005,
        Level = LogLevel.Error,
        Message = "Error joining game {GameCode}")]
    public static partial void LogGameJoinError(this ILogger logger, Exception ex, string gameCode);

    [LoggerMessage(
        EventId = 9006,
        Level = LogLevel.Error,
        Message = "Error playing card in game {GameId}")]
    public static partial void LogGamePlayCardError(this ILogger logger, Exception ex, Guid gameId);

    [LoggerMessage(
        EventId = 9007,
        Level = LogLevel.Error,
        Message = "Error drawing card in game {GameId}")]
    public static partial void LogGameDrawCardError(this ILogger logger, Exception ex, Guid gameId);

    [LoggerMessage(
        EventId = 9008,
        Level = LogLevel.Error,
        Message = "Error ending turn in game {GameId}")]
    public static partial void LogGameEndTurnError(this ILogger logger, Exception ex, Guid gameId);

    [LoggerMessage(
        EventId = 9009,
        Level = LogLevel.Error,
        Message = "Error discarding cards in game {GameId}")]
    public static partial void LogGameDiscardingError(this ILogger logger, Exception ex, Guid gameId);

    // Warning Logging

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "Player {PlayerId} disconnected during active game")]
    public static partial void LogPlayerDisconnectedWarning(this ILogger logger, Guid playerId);

    // Debug Logging

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "Player {PlayerId} score updated to {Score}")]
    public static partial void LogPlayerScoreUpdate(this ILogger logger, Guid playerId, int score);


    // Informational Logging

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Game {GameId} was successfully retrieved")]
    public static partial void LogGameRetrieved(this ILogger logger, Guid gameId);

    [LoggerMessage(
    EventId = 1002,
    Level = LogLevel.Information,
    Message = "Game created via API: {GameCode}")]
    public static partial void LogGameCreated(this ILogger logger, string gameCode);

    [LoggerMessage(
    EventId = 1003,
    Level = LogLevel.Information,
    Message = "Game {GameId} started with {PlayerCount} players")]
    public static partial void LogGameStarted(this ILogger logger, Guid gameId, int playerCount);

    [LoggerMessage(
    EventId = 1004,
    Level = LogLevel.Information,
    Message = "Game {GameId} ended")]
    public static partial void LogGameEnded(this ILogger logger, Guid gameId);

    [LoggerMessage(
    EventId = 1005,
    Level = LogLevel.Information,
    Message = "Game {GameId} abandoned")]
    public static partial void LogGameAbandoned(this ILogger logger, Guid gameId);

    [LoggerMessage(
    EventId = 1006,
    Level = LogLevel.Information,
    Message = "Player {PlayerName} joined game {GameId}")]
    public static partial void LogPlayerJoined(this ILogger logger, string playerName, Guid gameId);

    [LoggerMessage(
    EventId = 1007,
    Level = LogLevel.Information,
    Message = "Player {PlayerId} removed from game {GameId}")]
    public static partial void LogPlayerRemoved(this ILogger logger, Guid playerId, Guid gameId);

    [LoggerMessage(
    EventId = 1008,
    Level = LogLevel.Information,
    Message = "Game {GameId} settings updated")]
    public static partial void LogSettingsUpdated(this ILogger logger, Guid gameId);

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
