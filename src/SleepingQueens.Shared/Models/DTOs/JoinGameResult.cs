namespace SleepingQueens.Shared.Models.DTOs;

public class JoinGameResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid GameId { get; set; }
    public Guid JoinedPlayerId { get; set; }
    public string JoinedPlayerName { get; set; } = string.Empty;
    public int TotalPlayers { get; set; }
    public GameStateDto? GameState { get; set; }

    public static JoinGameResult SuccessResult(
        Guid gameId,
        Guid playerId,
        string playerName,
        int totalPlayers,
        GameStateDto gameState)
    {
        return new JoinGameResult
        {
            Success = true,
            GameId = gameId,
            JoinedPlayerId = playerId,
            JoinedPlayerName = playerName,
            TotalPlayers = totalPlayers,
            GameState = gameState
        };
    }

    public static JoinGameResult Error(string errorMessage)
    {
        return new JoinGameResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

    public ApiResponse<JoinGameResultDto> ToApiResponse()
    {
        if (Success)
        {
            var dto = new JoinGameResultDto
            {
                GameId = GameId,
                PlayerId = JoinedPlayerId,
                GameState = GameState
            };
            return ApiResponse<JoinGameResultDto>.SuccessResponse(dto);
        }

        return ApiResponse<JoinGameResultDto>.ErrorResponse(ErrorMessage ?? "");
    }
}

// Simple DTO for the response
public class JoinGameResultDto
{
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public GameStateDto? GameState { get; set; }
}