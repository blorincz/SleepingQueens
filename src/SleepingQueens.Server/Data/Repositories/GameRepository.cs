using Microsoft.EntityFrameworkCore;
using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Server.Data.Repositories;

public class GameRepository(ApplicationDbContext context) : BaseRepository<Game>(context), IGameRepository
{
    public async Task<Game?> GetByCodeAsync(string code)
    {
        return await _context.Games
            .FirstOrDefaultAsync(g => g.Code == code);
    }

    public async Task<IEnumerable<Game>> GetActiveGamesAsync()
    {
        return await _context.Games
            .Where(g => g.Status == GameStatus.Waiting || g.Status == GameStatus.Active)
            .Include(g => g.Players)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetGamesByStatusAsync(GameStatus status)
    {
        return await _context.Games
            .Where(g => g.Status == status)
            .Include(g => g.Players)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetGamesByPlayerIdAsync(Guid playerId)
    {
        return await _context.Games
            .Where(g => g.Players.Any(p => p.Id == playerId))
            .Include(g => g.Players)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(string code)
    {
        return await _context.Games.AnyAsync(g => g.Code == code);
    }

    public async Task<string> GenerateUniqueCodeAsync()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = new string([.. Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)])]);

            if (!await CodeExistsAsync(code))
                return code;
        }

        throw new InvalidOperationException("Could not generate unique game code");
    }

    // Player operations
    public async Task<Player?> GetPlayerAsync(Guid playerId)
    {
        return await _context.Players
            .Include(p => p.PlayerCards)
                .ThenInclude(pc => pc.Card)
            .Include(p => p.Queens)
            .FirstOrDefaultAsync(p => p.Id == playerId);
    }

    public async Task<Player> AddPlayerAsync(Guid gameId, Player player)
    {
        var game = await _context.Games.FindAsync(gameId) ?? throw new ArgumentException($"Game with ID {gameId} not found");
        if (game.Players.Count >= game.MaxPlayers)
            throw new InvalidOperationException("Game is full");

        player.GameId = gameId;
        _context.Players.Add(player);
        await _context.SaveChangesAsync();

        return player;
    }

    public async Task RemovePlayerAsync(Guid playerId)
    {
        var player = await GetPlayerAsync(playerId);
        if (player != null)
        {
            // Move player's cards to discard
            foreach (var playerCard in player.PlayerCards.ToList())
            {
                await DiscardCardAsync(player.GameId, playerCard.CardId);
                _context.PlayerCards.Remove(playerCard);
            }

            // Return player's queens to sleeping pool
            foreach (var queen in player.Queens.ToList())
            {
                queen.PlayerId = null;
                queen.IsAwake = false;
            }

            _context.Players.Remove(player);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdatePlayerTurnAsync(Guid gameId, Guid playerId, bool isCurrentTurn)
    {
        // Reset all players in game
        await _context.Players
            .Where(p => p.GameId == gameId)
            .ForEachAsync(p => p.IsCurrentTurn = false);

        // Set current player
        var player = await _context.Players.FindAsync(playerId);
        if (player != null)
        {
            player.IsCurrentTurn = isCurrentTurn;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Player>> GetPlayersInGameAsync(Guid gameId)
    {
        return await _context.Players
            .Where(p => p.GameId == gameId)
            .Include(p => p.PlayerCards)
                .ThenInclude(pc => pc.Card)
            .Include(p => p.Queens)
            .OrderBy(p => p.JoinedAt)
            .ToListAsync();
    }

    // Card operations
    public async Task<IEnumerable<Card>> GetPlayerHandAsync(Guid playerId)
    {
        return await _context.PlayerCards
            .Where(pc => pc.PlayerId == playerId)
            .OrderBy(pc => pc.HandPosition)
            .Select(pc => pc.Card)
            .ToListAsync();
    }

    public async Task AddCardToPlayerHandAsync(Guid playerId, Guid cardId)
    {
        var nextPosition = await _context.PlayerCards
            .Where(pc => pc.PlayerId == playerId)
            .MaxAsync(pc => (int?)pc.HandPosition) ?? -1;

        var playerCard = new PlayerCard
        {
            PlayerId = playerId,
            CardId = cardId,
            HandPosition = nextPosition + 1
        };

        _context.PlayerCards.Add(playerCard);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveCardFromPlayerHandAsync(Guid playerId, Guid cardId)
    {
        var playerCard = await _context.PlayerCards
            .FirstOrDefaultAsync(pc => pc.PlayerId == playerId && pc.CardId == cardId);

        if (playerCard != null)
        {
            _context.PlayerCards.Remove(playerCard);

            // Reorder remaining cards
            var remainingCards = await _context.PlayerCards
                .Where(pc => pc.PlayerId == playerId && pc.HandPosition > playerCard.HandPosition)
                .ToListAsync();

            foreach (var card in remainingCards)
            {
                card.HandPosition--;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task<GameCard?> DrawCardFromDeckAsync(Guid gameId)
    {
        var card = await _context.GameCards
            .Include(gc => gc.Card)
            .Where(gc => gc.GameId == gameId && gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.Position)
            .FirstOrDefaultAsync();

        if (card != null)
        {
            _context.GameCards.Remove(card);
            await _context.SaveChangesAsync();
        }

        return card;
    }

    public async Task ReturnCardToDeckAsync(Guid gameId, Guid cardId)
    {
        var maxPosition = await _context.GameCards
            .Where(gc => gc.GameId == gameId && gc.Location == CardLocation.Deck)
            .MaxAsync(gc => (int?)gc.Position) ?? -1;

        var gameCard = new GameCard
        {
            GameId = gameId,
            CardId = cardId,
            Location = CardLocation.Deck,
            Position = maxPosition + 1
        };

        _context.GameCards.Add(gameCard);
        await _context.SaveChangesAsync();
    }

    public async Task DiscardCardAsync(Guid gameId, Guid cardId)
    {
        var maxPosition = await _context.GameCards
            .Where(gc => gc.GameId == gameId && gc.Location == CardLocation.Discard)
            .MaxAsync(gc => (int?)gc.Position) ?? -1;

        var gameCard = new GameCard
        {
            GameId = gameId,
            CardId = cardId,
            Location = CardLocation.Discard,
            Position = maxPosition + 1
        };

        _context.GameCards.Add(gameCard);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<GameCard>> GetDeckCardsAsync(Guid gameId)
    {
        return await _context.GameCards
            .Include(gc => gc.Card)
            .Where(gc => gc.GameId == gameId && gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<GameCard>> GetDiscardPileAsync(Guid gameId)
    {
        return await _context.GameCards
            .Include(gc => gc.Card)
            .Where(gc => gc.GameId == gameId && gc.Location == CardLocation.Discard)
            .OrderBy(gc => gc.Position)
            .ToListAsync();
    }

    // Queen operations
    public async Task<IEnumerable<Queen>> GetSleepingQueensAsync(Guid gameId)
    {
        return await _context.Queens
            .Where(q => q.GameId == gameId && !q.IsAwake && q.PlayerId == null)
            .ToListAsync();
    }

    public async Task<IEnumerable<Queen>> GetPlayerQueensAsync(Guid playerId)
    {
        return await _context.Queens
            .Where(q => q.PlayerId == playerId && q.IsAwake)
            .ToListAsync();
    }

    public async Task<Queen?> GetQueenByIdAsync(Guid queenId)
    {
        return await _context.Queens
            .Include(q => q.Player)
            .FirstOrDefaultAsync(q => q.Id == queenId);
    }

    public async Task TransferQueenAsync(Guid queenId, Guid toPlayerId)
    {
        var queen = await GetQueenByIdAsync(queenId) ?? throw new ArgumentException($"Queen with ID {queenId} not found");
        queen.PlayerId = toPlayerId;
        queen.IsAwake = true;

        await _context.SaveChangesAsync();
    }

    public async Task PutQueenToSleepAsync(Guid queenId)
    {
        var queen = await GetQueenByIdAsync(queenId) ?? throw new ArgumentException($"Queen with ID {queenId} not found");
        queen.PlayerId = null;
        queen.IsAwake = false;

        await _context.SaveChangesAsync();
    }

    public async Task WakeQueenAsync(Guid queenId, Guid playerId)
    {
        var queen = await GetQueenByIdAsync(queenId) ?? throw new ArgumentException($"Queen with ID {queenId} not found");
        queen.PlayerId = playerId;
        queen.IsAwake = true;

        await _context.SaveChangesAsync();
    }

    // Move operations
    public async Task<Move> RecordMoveAsync(Move move)
    {
        _context.Moves.Add(move);
        await _context.SaveChangesAsync();
        return move;
    }

    public async Task<IEnumerable<Move>> GetGameMovesAsync(Guid gameId, int count = 50)
    {
        return await _context.Moves
            .Include(m => m.Player)
            .Where(m => m.GameId == gameId)
            .OrderByDescending(m => m.TurnNumber)
            .ThenByDescending(m => m.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> GetNextTurnNumberAsync(Guid gameId)
    {
        var lastTurn = await _context.Moves
            .Where(m => m.GameId == gameId)
            .MaxAsync(m => (int?)m.TurnNumber) ?? 0;

        return lastTurn + 1;
    }

    // Complex operations
    public async Task<GameState> GetFullGameStateAsync(Guid gameId)
    {
        var game = await _context.Games
            .Include(g => g.Players)
                .ThenInclude(p => p.PlayerCards)
                .ThenInclude(pc => pc.Card)
            .Include(g => g.Players)
                .ThenInclude(p => p.Queens)
            .Include(g => g.Queens)
            .Include(g => g.DeckCards)
                .ThenInclude(gc => gc.Card)
            .Include(g => g.Moves)
            .FirstOrDefaultAsync(g => g.Id == gameId) ?? throw new ArgumentException($"Game with ID {gameId} not found");
        return new GameState
        {
            Game = game,
            Players = [.. game.Players],
            Queens = [.. game.Queens],
            Deck = [.. game.DeckCards.Where(gc => gc.Location == CardLocation.Deck)],
            Discard = [.. game.DeckCards.Where(gc => gc.Location == CardLocation.Discard)],
            Moves = [.. game.Moves.OrderByDescending(m => m.Timestamp).Take(10)]
        };
    }

    // Add method to update settings
    public async Task UpdateGameSettingsAsync(Guid gameId, GameSettings settings)
    {
        var game = await GetByIdAsync(gameId) ?? throw new ArgumentException($"Game with ID {gameId} not found");

        // Validate settings
        if (!settings.Validate())
            throw new ArgumentException("Invalid game settings");

        game.Settings = settings;
        await _context.SaveChangesAsync();
    }

    // Update InitializeNewGameAsync to use settings
    public async Task InitializeNewGameAsync(Game game, Player firstPlayer)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Validate settings
            if (!game.Settings.Validate())
                throw new ArgumentException("Invalid game settings");

            // Add game
            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            // Add first player
            firstPlayer.GameId = game.Id;
            firstPlayer.IsCurrentTurn = true;
            _context.Players.Add(firstPlayer);
            await _context.SaveChangesAsync();

            // Add AI players if configured
            if (game.Settings.AllowAI && game.Settings.AICount > 0)
            {
                for (int i = 0; i < game.Settings.AICount; i++)
                {
                    var aiPlayer = new Player
                    {
                        GameId = game.Id,
                        Name = $"AI Player {i + 1}",
                        Type = PlayerType.AI_Medium, // Default to medium
                        JoinedAt = DateTime.UtcNow
                    };

                    _context.Players.Add(aiPlayer);
                }
                await _context.SaveChangesAsync();
            }

            // Get cards based on settings
            var cards = await GetCardsForSettingsAsync(game.Settings);

            // Add cards to deck
            var random = new Random();
            var shuffledCards = cards.OrderBy(c => random.Next()).ToList();

            for (int i = 0; i < shuffledCards.Count; i++)
            {
                var gameCard = new GameCard
                {
                    GameId = game.Id,
                    CardId = shuffledCards[i].Id,
                    Position = i,
                    Location = CardLocation.Deck
                };
                _context.GameCards.Add(gameCard);
            }

            // Get all queen cards
            var queenCards = await _context.Cards
                .Where(c => c.Type == CardType.Queen)
                .ToListAsync();

            // Add queens to sleeping pool
            foreach (var queenCard in queenCards)
            {
                var queenType = queenCard.Name switch
                {
                    "Rose Queen" => QueenType.RoseQueen,
                    "Cat Queen" => QueenType.CatQueen,
                    "Dog Queen" => QueenType.DogQueen,
                    "Peacock Queen" => QueenType.PeacockQueen,
                    "Rainbow Queen" => QueenType.RainbowQueen,
                    "Moon Queen" => QueenType.MoonQueen,
                    "Sun Queen" => QueenType.SunQueen,
                    "Star Queen" => QueenType.StarQueen,
                    "Cake Queen" => QueenType.CakeQueen,
                    "Heart Queen" => QueenType.HeartQueen,
                    _ => QueenType.RoseQueen
                };

                var queen = new Queen
                {
                    GameId = game.Id,
                    Name = queenCard.Name,
                    PointValue = queenCard.Value,
                    ImagePath = queenCard.ImagePath,
                    IsAwake = false,
                    Type = queenType
                };
                _context.Queens.Add(queen);
            }

            // Draw initial hand for all players
            var players = await GetPlayersInGameAsync(game.Id);
            foreach (var player in players)
            {
                for (int i = 0; i < game.Settings.StartingHandSize; i++)
                {
                    var gameCard = await DrawCardFromDeckAsync(game.Id);
                    if (gameCard != null)
                    {
                        await AddCardToPlayerHandAsync(player.Id, gameCard.CardId);
                    }
                }
            }

            // Record initial move
            var initialMove = new Move
            {
                GameId = game.Id,
                PlayerId = firstPlayer.Id,
                Type = MoveType.DrawCard,
                Description = $"Game started with {players.Count()} players",
                TurnNumber = 1,
                Timestamp = DateTime.UtcNow
            };
            await RecordMoveAsync(initialMove);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<List<Card>> GetCardsForSettingsAsync(GameSettings settings)
    {
        var query = _context.Cards.AsQueryable();

        // Filter based on settings
        if (!settings.EnableSpecialCards)
        {
            // Only include number cards
            query = query.Where(c => c.Type == CardType.Number);
        }
        else
        {
            // Apply custom counts from settings
            // This is simplified - you'd need to implement card selection based on counts
            var allCards = await query.ToListAsync();
            var selectedCards = new List<Card>();

            // Add number cards
            var numberCards = allCards.Where(c => c.Type == CardType.Number).ToList();
            selectedCards.AddRange(numberCards.Take(settings.NumberCardCountPerValue * 10));

            // Add special cards based on settings
            if (settings.KingCardCount > 0)
                selectedCards.AddRange(allCards.Where(c => c.Type == CardType.King).Take(settings.KingCardCount));

            if (settings.KnightCardCount > 0 && settings.AllowQueenStealing)
                selectedCards.AddRange(allCards.Where(c => c.Type == CardType.Knight).Take(settings.KnightCardCount));

            if (settings.DragonCardCount > 0 && settings.AllowDragonProtection)
                selectedCards.AddRange(allCards.Where(c => c.Type == CardType.Dragon).Take(settings.DragonCardCount));

            if (settings.SleepingPotionCount > 0 && settings.AllowSleepingPotions)
                selectedCards.AddRange(allCards.Where(c => c.Type == CardType.SleepingPotion).Take(settings.SleepingPotionCount));

            if (settings.JesterCardCount > 0 && settings.AllowJester)
                selectedCards.AddRange(allCards.Where(c => c.Type == CardType.Jester).Take(settings.JesterCardCount));

            return selectedCards;
        }

        return await query.ToListAsync();
    }

    public async Task ShuffleDeckAsync(Guid gameId)
    {
        var deckCards = await GetDeckCardsAsync(gameId);
        var random = new Random();
        var shuffled = deckCards.OrderBy(c => random.Next()).ToList();

        for (int i = 0; i < shuffled.Count; i++)
        {
            shuffled[i].Position = i;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> CheckForWinnerAsync(Guid gameId)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            return false;

        var players = await GetPlayersInGameAsync(gameId);
        var winner = players.FirstOrDefault(p => p.Score >= game.TargetScore);

        return winner != null;
    }

    public async Task EndGameAsync(Guid gameId, Guid? winnerId = null)
    {
        var game = await _context.Games.FindAsync(gameId);
        if (game == null)
            return;

        game.Status = GameStatus.Completed;
        game.Phase = GamePhase.Ended;
        game.EndedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}

// Helper class for full game state
public class GameState
{
    public required Game Game { get; set; }
    public required List<Player> Players { get; set; }
    public required List<Queen> Queens { get; set; }
    public required List<GameCard> Deck { get; set; }
    public required List<GameCard> Discard { get; set; }
    public required List<Move> Moves { get; set; }
}