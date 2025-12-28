namespace SleepingQueens.Shared.Models.DTOs;

public class AddAIPlayerResult
{
    public bool IsSuccess { get; set; }  // Renamed from Success
    public string? ErrorMessage { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int TotalPlayers { get; set; }

    public static AddAIPlayerResult Ok(Guid playerId, string playerName, int totalPlayers)
    {
        return new AddAIPlayerResult
        {
            IsSuccess = true,
            PlayerId = playerId,
            PlayerName = playerName,
            TotalPlayers = totalPlayers
        };
    }

    public static AddAIPlayerResult Fail(string errorMessage)
    {
        return new AddAIPlayerResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }

    public ApiResponse ToApiResponse()
    {
        return IsSuccess
            ? ApiResponse.SuccessResponse()
            : ApiResponse.ErrorResponse(ErrorMessage ?? "");
    }
}