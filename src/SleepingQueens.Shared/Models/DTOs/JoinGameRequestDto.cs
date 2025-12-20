namespace SleepingQueens.Shared.Models.DTOs;

public class JoinGameRequestDto
{
    public string GameCode { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
}
