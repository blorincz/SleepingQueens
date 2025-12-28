namespace SleepingQueens.Shared.Models.DTOs;

public class DrawCardResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public Guid CardId { get; set; }
    public string CardName { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }

    public static DrawCardResult SuccessResult(
        Guid cardId,
        string cardName,
        GameStateDto gameState)
    {
        return new DrawCardResult
        {
            Success = true,
            CardId = cardId,
            CardName = cardName,
            GameState = gameState
        };
    }

    public static DrawCardResult Error(string errorMessage)
    {
        return new DrawCardResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    public ApiResponse<DrawCardResultDto> ToApiResponse()
    {
        if (Success)
        {
            var dto = new DrawCardResultDto
            {
                CardId = CardId,
                CardName = CardName,
                GameState = GameState
            };
            return ApiResponse<DrawCardResultDto>.SuccessResponse(dto);
        }

        return ApiResponse<DrawCardResultDto>.ErrorResponse(ErrorMessage ?? "");
    }
}

public class DrawCardResultDto
{
    public Guid CardId { get; set; }
    public string CardName { get; set; } = string.Empty;
    public GameStateDto? GameState { get; set; }
}

