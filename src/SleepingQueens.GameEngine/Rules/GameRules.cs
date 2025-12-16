using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.GameEngine;

public static class GameRules
{
    public const int MaxPlayers = 6;
    public const int MinPlayers = 2;
    public const int StartingHandSize = 5;
    public const int DefaultTargetScore = 40;

    public static bool CanPlayKing(Card card, GameState state, Player player,
        out string? errorMessage)
    {
        errorMessage = null;

        if (!state.CanPlayCard(card, player))
        {
            errorMessage = "Cannot play this card";
            return false;
        }

        if (state.SleepingQueens.Count == 0)
        {
            errorMessage = "No sleeping queens to wake";
            return false;
        }

        return true;
    }

    public static bool CanPlayKnight(Card card, GameState state, Player player,
        Guid targetPlayerId, out string? errorMessage)
    {
        errorMessage = null;

        if (!state.CanPlayCard(card, player))
        {
            errorMessage = "Cannot play this card";
            return false;
        }

        var targetPlayer = state.Players.FirstOrDefault(p => p.Id == targetPlayerId);
        if (targetPlayer == null)
        {
            errorMessage = "Target player not found";
            return false;
        }

        if (targetPlayer.Id == player.Id)
        {
            errorMessage = "Cannot steal from yourself";
            return false;
        }

        if (targetPlayer.Queens.Count == 0)
        {
            errorMessage = "Target player has no queens to steal";
            return false;
        }

        // Check if target has dragon protection
        var targetHasDragon = targetPlayer.PlayerCards
            .Any(pc => pc.Card.Type == CardType.Dragon);

        if (targetHasDragon)
        {
            errorMessage = "Target player has dragon protection";
            return false;
        }

        return true;
    }

    public static bool CanPlayNumberCards(Card card1, Card card2, GameState state,
        Player player, out string? errorMessage)
    {
        errorMessage = null;

        // Must have both cards
        if (!player.PlayerCards.Any(pc => pc.CardId == card1.Id) ||
            !player.PlayerCards.Any(pc => pc.CardId == card2.Id))
        {
            errorMessage = "Player does not have both cards";
            return false;
        }

        // Must be same value
        if (card1.Value != card2.Value)
        {
            errorMessage = "Number cards must have the same value";
            return false;
        }

        return true;
    }

    public static bool CanPlayNumberCardRun(Card[] cards, GameState state,
        Player player, out string? errorMessage)
    {
        errorMessage = null;

        if (cards.Length < 3)
        {
            errorMessage = "Need at least 3 cards for a run";
            return false;
        }

        // Check player has all cards
        foreach (var card in cards)
        {
            if (!player.PlayerCards.Any(pc => pc.CardId == card.Id))
            {
                errorMessage = $"Player does not have card: {card.Name}";
                return false;
            }
        }

        // Check consecutive values
        var ordered = cards.OrderBy(c => c.Value).ToArray();
        for (int i = 1; i < ordered.Length; i++)
        {
            if (ordered[i].Value != ordered[i - 1].Value + 1)
            {
                errorMessage = "Cards must be consecutive numbers";
                return false;
            }
        }

        return true;
    }

    public static bool CheckForWinner(GameState state, out Player? winner)
    {
        winner = null;

        foreach (var player in state.Players)
        {
            var score = player.Queens.Sum(q => q.PointValue);
            if (score >= state.Game.TargetScore)
            {
                winner = player;
                return true;
            }
        }

        return false;
    }
}