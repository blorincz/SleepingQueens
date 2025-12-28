using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class PlayerDisconnectResult
{
    public bool ShouldNotifyPlayers { get; set; }
    public string? NotificationMessage { get; set; }
    public Guid GameId { get; set; }
    public string? PlayerName { get; set; }
    public bool CanReconnect { get; set; }
    public bool IsGameActive { get; set; }
    public DisconnectAction ActionTaken { get; set; }

    // Factory methods
    public static PlayerDisconnectResult PlayerRemoved(
        Guid gameId, string playerName, bool wasHost, string message)
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = true,
            GameId = gameId,
            PlayerName = playerName,
            NotificationMessage = wasHost
                ? $"{playerName} (host) left the game"
                : message,
            CanReconnect = false,
            IsGameActive = false,
            ActionTaken = DisconnectAction.PlayerRemoved
        };
    }

    public static PlayerDisconnectResult PlayerDisconnected(
        Guid gameId, string playerName, bool canReconnect, string message)
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = true,
            GameId = gameId,
            PlayerName = playerName,
            NotificationMessage = message,
            CanReconnect = canReconnect,
            IsGameActive = true,
            ActionTaken = DisconnectAction.PlayerDisconnected
        };
    }

    public static PlayerDisconnectResult GameEnded(
        Guid gameId, string playerName, string message)
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = true,
            GameId = gameId,
            PlayerName = playerName,
            NotificationMessage = message,
            CanReconnect = false,
            IsGameActive = false,
            ActionTaken = DisconnectAction.GameEnded
        };
    }

    public static PlayerDisconnectResult NoActionNeeded(Guid gameId, string playerName)
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = false,
            GameId = gameId,
            PlayerName = playerName,
            CanReconnect = false,
            IsGameActive = false,
            ActionTaken = DisconnectAction.None
        };
    }

    public static PlayerDisconnectResult PlayerNotFound()
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = false,
            ActionTaken = DisconnectAction.None
        };
    }

    public static PlayerDisconnectResult GameNotFound()
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = false,
            ActionTaken = DisconnectAction.None
        };
    }

    public static PlayerDisconnectResult Error(string errorMessage)
    {
        return new PlayerDisconnectResult
        {
            ShouldNotifyPlayers = false,
            NotificationMessage = errorMessage,
            ActionTaken = DisconnectAction.Error
        };
    }
}
