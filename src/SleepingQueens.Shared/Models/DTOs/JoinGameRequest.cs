namespace SleepingQueens.Shared.Models.DTOs;

public class JoinGameRequest
{
    public string GameCode { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
}
