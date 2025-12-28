namespace SleepingQueens.Shared.Models.DTOs;

public class StartGameResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static StartGameResult SuccessResult() => new() { Success = true };
    public static StartGameResult Error(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    public ApiResponse ToApiResponse()
    {
        return Success
            ? ApiResponse.SuccessResponse()
            : ApiResponse.ErrorResponse(ErrorMessage ?? "");
    }
}
