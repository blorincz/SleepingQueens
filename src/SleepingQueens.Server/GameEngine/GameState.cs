using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Server.GameEngine;

public class GameState
{
    public required Game Game { get; set; }
    public required List<Player> Players { get; set; }
    public required List<Queen> SleepingQueens { get; set; }
    public required List<Queen> AwakenedQueens { get; set; }
    public required Deck Deck { get; set; }
    public required List<Card> DiscardPile { get; set; }
    public required List<Move> RecentMoves { get; set; }
    public required Player? CurrentPlayer { get; set; }
    public required GamePhase CurrentPhase { get; set; }

    // Helper properties
    public bool IsGameOver => Game.Status == GameStatus.Completed;
    public Player? Winner => Players.OrderByDescending(p => p.Score).FirstOrDefault();
    public int CardsInDeck => Deck.Count;

    // Validation helpers
    public bool CanPlayCard(Card card, Player player)
    {
        if (CurrentPlayer?.Id != player.Id) return false;
        if (!player.PlayerCards.Any(pc => pc.CardId == card.Id)) return false;

        // Add card-specific validation here
        return true;
    }
}