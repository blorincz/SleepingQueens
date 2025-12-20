using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Shared.Models.DTOs;

public class GameStateDto
{
    public required GameDto Game { get; set; }
    public required List<PlayerDto> Players { get; set; }
    public required List<QueenDto> SleepingQueens { get; set; }
    public required List<QueenDto> AwakenedQueens { get; set; }
    public required List<CardDto> DeckCards { get; set; }
    public required List<CardDto> DiscardPile { get; set; }
    public required List<MoveDto> RecentMoves { get; set; }
    public required PlayerDto? CurrentPlayer { get; set; }
    public required GamePhase CurrentPhase { get; set; }

    // Computed properties
    public bool IsGameOver => Game.Status == GameStatus.Completed || Game.Status == GameStatus.Abandoned;
    public PlayerDto? Winner => Players.OrderByDescending(p => p.Score).FirstOrDefault(p => p.Score >= Game.TargetScore);
    public int CardsInDeck => DeckCards.Count;

    // Helper methods
    public bool CanPlayCard(CardDto card, PlayerDto player)
    {
        if (CurrentPlayer?.Id != player.Id) return false;
        if (!player.Hand.Any(c => c.Id == card.Id)) return false;
        return true;
    }
}
