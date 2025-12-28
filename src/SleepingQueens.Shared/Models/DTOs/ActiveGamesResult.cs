namespace SleepingQueens.Shared.Models.DTOs;

public class ActiveGamesResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IEnumerable<ActiveGameInfo> Games { get; set; } = [];

    public static ActiveGamesResult SuccessResult(IEnumerable<ActiveGameInfo> games)
        => new() { Success = true, Games = games };

    public static ActiveGamesResult Error(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };
}
