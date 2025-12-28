namespace SleepingQueens.Shared.Models.DTOs;
public class RemovePlayerResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RemovedPlayerName { get; set; }

    public static RemovePlayerResult SuccessResult(string playerName)
        => new() { Success = true, RemovedPlayerName = playerName };

    public static RemovePlayerResult Error(string errorMessage)
        => new() { Success = false, ErrorMessage = errorMessage };

    public ApiResponse ToApiResponse()
        => Success ? ApiResponse.SuccessResponse() : ApiResponse.ErrorResponse(ErrorMessage ?? "");
}
