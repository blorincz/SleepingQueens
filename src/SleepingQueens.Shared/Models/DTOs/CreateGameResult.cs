namespace SleepingQueens.Shared.Models.DTOs;

public class CreateGameResult
{
    public bool Success { get; set; }
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public string? ErrorMessage { get; set; }

    public static CreateGameResult SuccessResult(
        Guid gameId,
        string gameCode,
        Guid playerId)
    {
        return new CreateGameResult
        {
            Success = true,
            GameId = gameId,
            GameCode = gameCode,
            PlayerId = playerId
        };
    }

    public static CreateGameResult Error(string errorMessage)
    {
        return new CreateGameResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    public ApiResponse<CreateGameResultDto> ToApiResponse()
    {
        if (Success)
        {
            var dto = new CreateGameResultDto
            {
                GameId = GameId,
                GameCode = GameCode,
                PlayerId = PlayerId
            };
            return ApiResponse<CreateGameResultDto>.SuccessResponse(dto);
        }

        return ApiResponse<CreateGameResultDto>.ErrorResponse(ErrorMessage ?? "");
    }
}

public class CreateGameResultDto
{
    public Guid GameId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
}