namespace SleepingQueens.Shared.Models.DTOs;

public class ReconnectPlayerResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PlayerName { get; set; }

    public static ReconnectPlayerResult SuccessResult(string playerName)
    {
        return new ReconnectPlayerResult
        {
            Success = true,
            PlayerName = playerName
        };
    }

    public static ReconnectPlayerResult Error(string errorMessage)
    {
        return new ReconnectPlayerResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    public ApiResponse ToApiResponse()
    {
        return Success
            ? ApiResponse.SuccessResponse()
            : ApiResponse.ErrorResponse(ErrorMessage ?? "Reconnection failed");
    }
}
