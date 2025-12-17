using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using System.Text.Json;

namespace SleepingQueens.Data.Mapping;

public static class GameStateMapper
{
    public static GameStateDto ToDto(Game game, List<Player> players, List<Queen> queens,
        List<GameCard> deckCards, List<Move> moves)
    {
        var sleepingQueens = queens
            .Where(q => !q.IsAwake && q.PlayerId == null)
            .ToList();

        var awakenedQueens = queens
            .Where(q => q.IsAwake && q.PlayerId != null)
            .ToList();

        var playerDtos = players.Select(ToDto).ToList();
        var playerDict = playerDtos.ToDictionary(p => p.Id);

        return new GameStateDto
        {
            Game = ToDto(game),
            Players = playerDtos,
            SleepingQueens = [.. sleepingQueens.Select(ToDto)],
            AwakenedQueens = [.. awakenedQueens.Select(ToDto)],
            DeckCards = [.. deckCards
                .Where(gc => gc.Location == CardLocation.Deck)
                .Select(gc => ToDto(gc.Card))],
            DiscardPile = [.. deckCards
                .Where(gc => gc.Location == CardLocation.Discard)
                .Select(gc => ToDto(gc.Card))],
            RecentMoves = [.. moves
                .OrderByDescending(m => m.Timestamp)
                .Take(10)
                .Select(m => ToDto(m,
                    m.Player != null && playerDict.TryGetValue(m.Player.Id, out var playerDto)
                        ? playerDto
                        : null))],
            CurrentPlayer = players.FirstOrDefault(p => p.IsCurrentTurn) is Player currentPlayer
                ? ToDto(currentPlayer)
                : null,
            CurrentPhase = game.Phase
        };
    }

    public static GameDto ToDto(Game game)
    {
        return new GameDto
        {
            Id = game.Id,
            Code = game.Code,
            Status = game.Status,
            Phase = game.Phase,
            TargetScore = game.TargetScore,
            MaxPlayers = game.MaxPlayers,
            Settings = game.Settings,
            CreatedAt = game.CreatedAt,
            StartedAt = game.StartedAt,
            EndedAt = game.EndedAt
        };
    }

    public static PlayerDto ToDto(Player player)
    {
        return new PlayerDto
        {
            Id = player.Id,
            Name = player.Name,
            Type = player.Type,
            Score = player.Score,
            IsCurrentTurn = player.IsCurrentTurn,
            Hand = [.. player.PlayerCards
                .OrderBy(pc => pc.HandPosition)
                .Select(pc => ToDto(pc.Card))],
            Queens = [.. player.Queens.Select(ToDto)]
        };
    }

    public static QueenDto ToDto(Queen queen)
    {
        return new QueenDto
        {
            Id = queen.Id,
            Name = queen.Name,
            PointValue = queen.PointValue,
            ImagePath = queen.ImagePath,
            PlayerId = queen.PlayerId,
            IsAwake = queen.IsAwake
        };
    }

    public static CardDto ToDto(Card card)
    {
        return new CardDto
        {
            Id = card.Id,
            Type = card.Type,
            Value = card.Value,
            Name = card.Name,
            Description = card.Description,
            ImagePath = card.ImagePath
        };
    }

    public static MoveDto ToDto(Move move, PlayerDto? player = null)
    {
        return new MoveDto
        {
            Id = move.Id,
            TurnNumber = move.TurnNumber,
            Type = move.Type,
            Description = move.Description,
            CardIds = !string.IsNullOrEmpty(move.CardIds)
                ? JsonSerializer.Deserialize<List<Guid>>(move.CardIds)
                : null,
            Target = !string.IsNullOrEmpty(move.TargetData)
                ? JsonSerializer.Deserialize<MoveTargetDto>(move.TargetData)
                : null,
            Player = player,
            Timestamp = move.Timestamp
        };
    }
}