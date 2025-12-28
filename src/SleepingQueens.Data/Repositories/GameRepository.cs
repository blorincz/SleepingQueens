using Microsoft.EntityFrameworkCore;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Data.Mapping;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Repositories;

public class GameRepository(ApplicationDbContext context) : BaseRepository<Game>(context), IGameRepository
{

    // ========== GAME OPERATIONS ==========

    public async Task<Game?> GetByCodeAsync(string code)
    {
        return await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Code == code);
    }

    public override async Task<Game?> GetByIdAsync(Guid Id)
    {
        return await _context.Games
            .Include(g => g.Players)
            .FirstOrDefaultAsync(g => g.Id == Id);
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

    // ========== PLAYER OPERATIONS ==========

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

    // ========== CARD OPERATIONS ==========

    public async Task<IEnumerable<Card>> GetCardByTypeAsync(CardType type)
    {
        return await _context.Cards
            .Where(c => c.Type == type)
            .ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetNumberCardsAsync()
    {
        return await _context.Cards
            .Where(c => c.Type == CardType.Number)
            .ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetSpecialCardsAsync()
    {
        return await _context.Cards
            .Where(c => c.Type != CardType.Number && c.Type != CardType.Queen)
            .ToListAsync();
    }

    public async Task<Card?> GetCardByValueAsync(CardType type, int value)
    {
        return await _context.Cards
            .FirstOrDefaultAsync(c => c.Type == type && c.Value == value);
    }

    public async Task<IEnumerable<Card>> GetCardsByIdsAsync(IEnumerable<Guid> cardIds)
    {
        return await _context.Cards
            .Where(c => cardIds.Contains(c.Id))
            .ToListAsync();
    }

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

    // ========== QUEEN OPERATIONS ==========

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

    public async Task<IEnumerable<Queen>> GetQueensForGameAsync(Guid gameId)
    {
        return await _context.Queens
            .Where(q => q.GameId == gameId)
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

    // ========== MOVE OPERATIONS ==========

    public async Task<Move> RecordMoveAsync(Move move)
    {
        _context.Moves.Add(move);
        await _context.SaveChangesAsync();
        return move;
    }

    public async Task<List<Move>> GetGameMovesAsync(Guid gameId, int limit = 50)
    {
        return await _context.Moves
            .Include(m => m.Player)
            .Where(m => m.GameId == gameId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetNextTurnNumberAsync(Guid gameId)
    {
        var lastTurn = await _context.Moves
            .Where(m => m.GameId == gameId)
            .MaxAsync(m => (int?)m.TurnNumber) ?? 0;

        return lastTurn + 1;
    }

    // ========== GAME STATE DTO OPERATIONS ==========

    public async Task<GameStateDto> GetGameStateDtoAsync(Guid gameId)
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
                .ThenInclude(m => m.Player)
            .FirstOrDefaultAsync(g => g.Id == gameId)
            ?? throw new ArgumentException($"Game with ID {gameId} not found");

        // Get recent moves
        var recentMoves = game.Moves
            .OrderByDescending(m => m.Timestamp)
            .Take(10)
            .ToList();

        // Use GameStateMapper with entity moves (Option A)
        return GameStateMapper.ToDto(
            game,
            [.. game.Players],
            [.. game.Queens],
            [.. game.DeckCards],
            recentMoves
        );
    }

    public async Task InitializeNewGameAsync(Game game, Player firstPlayer)
    {
        // Draw initial hand for first player (5 cards)
        for (int i = 0; i < 5; i++)
        {
            var card = await _context.GameCards
            .Include(gc => gc.Card)
            .Where(gc => gc.GameId == game.Id && gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.Position)
            .FirstOrDefaultAsync();

            if (card != null)
            {
                _context.GameCards.Remove(card);
                await _context.SaveChangesAsync();
            }

            //var gameCard = await DrawCardFromDeckAsync(game.Id);
            if (card != null)
            {
                await AddCardToPlayerHandAsync(firstPlayer.Id, card.CardId);
            }
        }

        // Record initial move
        var initialMove = new Move
        {
            GameId = game.Id,
            PlayerId = firstPlayer.Id,
            Type = MoveType.DrawCard,
            Description = $"Initial hand drawn for {firstPlayer.Name}",
            TurnNumber = 1,
            Timestamp = DateTime.UtcNow
        };
        await RecordMoveAsync(initialMove);

        //await transaction.CommitAsync();
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

    public async Task UpdateGameSettingsAsync(Guid gameId, GameSettings settings)
    {
        var game = await GetByIdAsync(gameId) ?? throw new ArgumentException($"Game with ID {gameId} not found");
        game.Settings = settings;
        await _context.SaveChangesAsync();
    }

    public async Task InitializeDeckAsync(Guid gameId, GameSettings settings)
    {
        // Get all standard cards (non-queens)
        var cards = await _context.Cards
            .Where(c => c.Type != CardType.Queen)
            .ToListAsync();

        // Add cards to deck
        var random = new Random();
        var shuffledCards = cards.OrderBy(c => random.Next()).ToList();

        for (int i = 0; i < shuffledCards.Count; i++)
        {
            var gameCard = new GameCard
            {
                GameId = gameId,
                CardId = shuffledCards[i].Id,
                Position = i,
                Location = CardLocation.Deck
            };
            _context.GameCards.Add(gameCard);
        }

        await _context.SaveChangesAsync();
    }

    Task<List<GameCard>> IGameRepository.GetPlayerHandAsync(Guid playerId)
    {
        throw new NotImplementedException();
    }

    Task<List<GameCard>> IGameRepository.GetDiscardPileAsync(Guid gameId)
    {
        throw new NotImplementedException();
    }

    Task<List<GameCard>> IGameRepository.GetDeckCardsAsync(Guid gameId)
    {
        throw new NotImplementedException();
    }

    Task<IEnumerable<Card>> IGameRepository.GetByTypeAsync(CardType type)
    {
        throw new NotImplementedException();
    }

    public async Task PlaceSleepingQueensAsync(Guid gameId, GameSettings settings)
    {
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
                "Sunflower Queen" => QueenType.SunflowerQueen,
                "Starfish Queen" => QueenType.StarfishQueen,
                "Cake Queen" => QueenType.CakeQueen,
                "Heart Queen" => QueenType.HeartQueen,
                "Pancake Queen" => QueenType.PancakeQueen,
                "Ladybug Queen" => QueenType.LadybugQueen,
                _ => QueenType.RoseQueen
            };

            var queen = new Queen
            {
                GameId = gameId,
                Name = queenCard.Name,
                PointValue = queenCard.Value,
                ImagePath = queenCard.ImagePath,
                IsAwake = false,
                Type = queenType
            };
            _context.Queens.Add(queen);
            await _context.SaveChangesAsync();
        }
    }
}