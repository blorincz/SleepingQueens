using Microsoft.EntityFrameworkCore;
using SleepingQueens.Data;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Tests.Helpers;

public class TestDataSeeder(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Game> SeedGameAsync(Game game)
    {
        _context.Games.Add(game);

        foreach (var player in game.Players)
        {
            _context.Players.Add(player);
        }

        await _context.SaveChangesAsync();
        return game;
    }

    public async Task<Player> SeedPlayerAsync(Player player)
    {
        if (player.GameId != Guid.Empty)
        {
            var gameExists = await _context.Games.AnyAsync(g => g.Id == player.GameId);
            if (!gameExists)
            {
                var game = new Game
                {
                    Id = player.GameId,
                    Code = $"TEST{new Random().Next(100000, 999999)}",
                    Status = GameStatus.Waiting,
                    Phase = GamePhase.Setup,
                    MaxPlayers = 4,
                    TargetScore = 40,
                    CreatedAt = DateTime.UtcNow,
                    Settings = GameSettings.Default
                };
                _context.Games.Add(game);
            }
        }

        _context.Players.Add(player);
        await _context.SaveChangesAsync();
        return player;
    }

    public async Task<Card> SeedCardAsync(Card card)
    {
        _context.Cards.Add(card);
        await _context.SaveChangesAsync();
        return card;
    }

    public async Task ClearAllDataAsync()
    {
        if (_context.Players != null)
            _context.Players.RemoveRange(_context.Players);

        if (_context.Games != null)
            _context.Games.RemoveRange(_context.Games);

        if (_context.Cards != null)
            _context.Cards.RemoveRange(_context.Cards);

        if (_context.Queens != null)
            _context.Queens.RemoveRange(_context.Queens);

        if (_context.GameCards != null)
            _context.GameCards.RemoveRange(_context.GameCards);

        if (_context.PlayerCards != null)
            _context.PlayerCards.RemoveRange(_context.PlayerCards);

        if (_context.Moves != null)
            _context.Moves.RemoveRange(_context.Moves);

        await _context.SaveChangesAsync();
    }

    public async Task<Game> CreateAndSeedCompleteGameAsync(
        string? gameCode = null,
        int playerCount = 2,
        GameStatus status = GameStatus.Waiting)
    {
        var game = new Game
        {
            Id = Guid.NewGuid(),
            Code = gameCode ?? $"TEST{new Random().Next(100000, 999999)}",
            Status = status,
            Phase = GamePhase.Setup,
            MaxPlayers = 4,
            TargetScore = 40,
            CreatedAt = DateTime.UtcNow,
            Settings = GameSettings.Default,
            Players = new List<Player>()
        };

        for (int i = 0; i < playerCount; i++)
        {
            var player = new Player
            {
                Id = Guid.NewGuid(),
                Name = $"Player{i + 1}",
                Type = PlayerType.Human,
                Score = 0,
                IsCurrentTurn = i == 0,
                GameId = game.Id,
                ConnectionId = $"test-conn-{i}"
            };
            game.Players.Add(player);
        }

        return await SeedGameAsync(game);
    }
}