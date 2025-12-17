using System.Text.Json;
using SleepingQueens.Server.Data.Repositories; // IGameRepository, ICardRepository
using SleepingQueens.Shared.Models.DTOs;       // DTOs
using SleepingQueens.Shared.Models.Game;       // Domain models
using SleepingQueens.Server.Mapping;
using SleepingQueens.Shared.Models.Game.Enums;           // GameStateMapper

namespace SleepingQueens.Server.GameEngine;

public class SleepingQueensGameEngine(
    IGameRepository gameRepository,
    ICardRepository cardRepository,
    ILogger<SleepingQueensGameEngine> logger) : IGameEngine
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ICardRepository _cardRepository = cardRepository;
    private readonly ILogger<SleepingQueensGameEngine> _logger = logger;

    // ========== GAME LIFECYCLE ==========

    public Game CreateGame(GameSettings settings, Player creator)
    {
        // Validate settings
        if (!settings.Validate())
            throw new ArgumentException("Invalid game settings");

        var game = new Game
        {
            Code = GenerateGameCode(),
            Status = GameStatus.Waiting,
            Phase = GamePhase.Setup,
            MaxPlayers = Math.Clamp(settings.MaxPlayers, 2, GameRules.MaxPlayers),
            TargetScore = settings.TargetScore,
            CreatedAt = DateTime.UtcNow,
            Settings = settings
        };

        creator.GameId = game.Id;
        creator.IsCurrentTurn = true;
        creator.JoinedAt = DateTime.UtcNow;

        return game;
    }

    public async Task<Game> StartGameAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        if (game.Players.Count < game.Settings.MinPlayers)
            throw new InvalidOperationException($"Need at least {game.Settings.MinPlayers} players to start");

        if (game.Players.Count > game.Settings.MaxPlayers)
            throw new InvalidOperationException($"Too many players. Maximum is {game.Settings.MaxPlayers}");

        game.Status = GameStatus.Active;
        game.Phase = GamePhase.Playing;
        game.StartedAt = DateTime.UtcNow;

        await _gameRepository.UpdateAsync(game);

        _logger.LogInformation("Game {GameId} started with {PlayerCount} players",
            gameId, game.Players.Count);

        return game;
    }

    public async Task<Game> EndGameAsync(Guid gameId, Guid? winnerId = null)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        game.Status = GameStatus.Completed;
        game.Phase = GamePhase.Ended;
        game.EndedAt = DateTime.UtcNow;

        await _gameRepository.UpdateAsync(game);

        _logger.LogInformation("Game {GameId} ended", gameId);

        return game;
    }

    public async Task<Game> AbandonGameAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        game.Status = GameStatus.Abandoned;
        game.Phase = GamePhase.Ended;
        game.EndedAt = DateTime.UtcNow;

        await _gameRepository.UpdateAsync(game);

        _logger.LogInformation("Game {GameId} abandoned", gameId);

        return game;
    }

    // ========== PLAYER MANAGEMENT ==========

    public async Task<Player> AddPlayerAsync(Guid gameId, Player player)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game == null)
            throw new ArgumentException($"Game {gameId} not found");

        if (game.IsFull())
            throw new InvalidOperationException("Game is full");

        player.GameId = gameId;
        player.JoinedAt = DateTime.UtcNow;

        var addedPlayer = await _gameRepository.AddPlayerAsync(gameId, player);

        _logger.LogInformation("Player {PlayerName} joined game {GameId}",
            player.Name, gameId);

        return addedPlayer;
    }

    public async Task<Player> AddAIPlayerAsync(Guid gameId, AILevel level = AILevel.Medium)
    {
        var player = new Player
        {
            Name = $"AI ({level})",
            Type = level switch
            {
                AILevel.Easy => PlayerType.AI_Easy,
                AILevel.Medium => PlayerType.AI_Medium,
                AILevel.Hard => PlayerType.AI_Hard,
                AILevel.Expert => PlayerType.AI_Hard,
                _ => PlayerType.AI_Medium
            }
        };

        return await AddPlayerAsync(gameId, player);
    }

    public async Task RemovePlayerAsync(Guid gameId, Guid playerId)
    {
        await _gameRepository.RemovePlayerAsync(playerId);

        _logger.LogInformation("Player {PlayerId} removed from game {GameId}",
            playerId, gameId);
    }

    public async Task UpdatePlayerScoreAsync(Guid playerId, int score)
    {
        var player = await _gameRepository.GetPlayerAsync(playerId);
        if (player == null)
            throw new ArgumentException($"Player {playerId} not found");

        player.Score = score;
        await _gameRepository.UpdateAsync(player.Game);

        _logger.LogDebug("Player {PlayerId} score updated to {Score}",
            playerId, score);
    }

    // ========== GAME ACTIONS ==========

    public async Task<GameActionResult> PlayCardAsync(Guid gameId, Guid playerId,
        Guid cardId, Guid? targetPlayerId = null, Guid? targetQueenId = null)
    {
        try
        {
            var state = await GetGameStateAsync(gameId);
            var player = state.Players.FirstOrDefault(p => p.Id == playerId);
            if (player == null)
                return new GameActionResult(false, "Player not found in game");

            var card = player.PlayerCards
                .Select(pc => pc.Card)
                .FirstOrDefault(c => c.Id == cardId);
            if (card == null)
                return new GameActionResult(false, "Player does not have this card");

            // Validate move
            if (!await IsValidMoveAsync(gameId, playerId, cardId, targetPlayerId, targetQueenId))
                return new GameActionResult(false, "Invalid move");

            // Handle different card types
            var result = await HandleCardInternalAsync(state, player, card, targetPlayerId, targetQueenId);

            if (result.Success)
            {
                // Record the move
                var move = new Move
                {
                    GameId = gameId,
                    PlayerId = playerId,
                    Type = MoveType.PlayCard,
                    Description = $"{player.Name} played {card.Name}",
                    CardIds = JsonSerializer.Serialize(new[] { cardId }),
                    TargetData = targetPlayerId.HasValue || targetQueenId.HasValue
                        ? JsonSerializer.Serialize(new { targetPlayerId, targetQueenId })
                        : null,
                    TurnNumber = await _gameRepository.GetNextTurnNumberAsync(gameId),
                    Timestamp = DateTime.UtcNow
                };

                await _gameRepository.RecordMoveAsync(move);

                // Check for winner
                var winner = await CheckForWinnerAsync(gameId);
                if (winner != null)
                {
                    await EndGameAsync(gameId, winner.Id);

                    // Update result with game end event
                    var gameEndEvent = new GameEventDto
                    {
                        Type = GameEventType.GameEnded,
                        Description = $"{winner.Name} wins the game with {winner.Score} points!",
                        Timestamp = DateTime.UtcNow,
                        Data = new { WinnerId = winner.Id, WinnerName = winner.Name }
                    };

                    var gameStateDto = await GetGameStateDtoAsync(gameId);
                    return new GameActionResult(true, result.Message, gameStateDto, gameEndEvent);
                }

                // Return successful result with DTO
                var updatedStateDto = await GetGameStateDtoAsync(gameId);
                return new GameActionResult(true, result.Message, updatedStateDto, result.GameEvent);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing card in game {GameId}", gameId);
            return new GameActionResult(false, $"Error: {ex.Message}");
        }
    }

    private async Task<GameActionResult> HandleCardInternalAsync(GameState state, Player player,
        Card card, Guid? targetPlayerId, Guid? targetQueenId)
    {
        return card.Type switch
        {
            CardType.King => await HandleKingCardAsync(state, player, card, targetQueenId),
            CardType.Knight => await HandleKnightCardAsync(state, player, card, targetPlayerId!.Value),
            CardType.Dragon => await HandleDragonCardAsync(state, player, card, targetPlayerId),
            CardType.SleepingPotion => await HandlePotionCardAsync(state, player, card, targetQueenId!.Value),
            CardType.Jester => await HandleJesterCardAsync(state, player, card),
            CardType.Number => await HandleNumberCardAsync(state, player, card),
            _ => new GameActionResult(false, $"Unhandled card type: {card.Type}")
        };
    }

    public async Task<GameActionResult> DrawCardAsync(Guid gameId, Guid playerId)
    {
        try
        {
            var state = await GetGameStateAsync(gameId);
            var player = state.Players.FirstOrDefault(p => p.Id == playerId);

            if (player == null)
                return new GameActionResult(false, "Player not found");

            if (state.CurrentPlayer?.Id != playerId)
                return new GameActionResult(false, "Not your turn");

            var drawnCard = await _gameRepository.DrawCardFromDeckAsync(gameId);
            if (drawnCard == null)
                return new GameActionResult(false, "Deck is empty");

            await _gameRepository.AddCardToPlayerHandAsync(playerId, drawnCard.CardId);

            // Record move
            var move = new Move
            {
                GameId = gameId,
                PlayerId = playerId,
                Type = MoveType.DrawCard,
                Description = $"{player.Name} drew a card",
                TurnNumber = await _gameRepository.GetNextTurnNumberAsync(gameId),
                Timestamp = DateTime.UtcNow
            };

            await _gameRepository.RecordMoveAsync(move);

            var gameStateDto = await GetGameStateDtoAsync(gameId);
            var gameEvent = new GameEventDto
            {
                Type = GameEventType.CardPlayed,
                Description = $"{player.Name} drew a card",
                Timestamp = DateTime.UtcNow
            };

            return new GameActionResult(true, "Card drawn", gameStateDto, gameEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing card in game {GameId}", gameId);
            return new GameActionResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<GameActionResult> EndTurnAsync(Guid gameId, Guid playerId)
    {
        try
        {
            var state = await GetGameStateAsync(gameId);
            var players = state.Players.OrderBy(p => p.JoinedAt).ToList();
            var currentIndex = players.FindIndex(p => p.Id == playerId);

            if (currentIndex == -1)
                return new GameActionResult(false, "Player not found in game");

            // Calculate next player index
            int nextIndex = (currentIndex + 1) % players.Count;

            // Update current player
            await _gameRepository.UpdatePlayerTurnAsync(gameId, players[currentIndex].Id, false);
            await _gameRepository.UpdatePlayerTurnAsync(gameId, players[nextIndex].Id, true);

            // Record move
            var move = new Move
            {
                GameId = gameId,
                PlayerId = playerId,
                Type = MoveType.EndTurn,
                Description = $"{players[currentIndex].Name} ended turn. {players[nextIndex].Name}'s turn.",
                TurnNumber = await _gameRepository.GetNextTurnNumberAsync(gameId),
                Timestamp = DateTime.UtcNow
            };

            await _gameRepository.RecordMoveAsync(move);

            var gameStateDto = await GetGameStateDtoAsync(gameId);
            var gameEvent = new GameEventDto
            {
                Type = GameEventType.TurnEnded,
                Description = $"{players[nextIndex].Name}'s turn now",
                Timestamp = DateTime.UtcNow,
                Data = new { NextPlayerId = players[nextIndex].Id, NextPlayerName = players[nextIndex].Name }
            };

            return new GameActionResult(true, "Turn ended", gameStateDto, gameEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending turn in game {GameId}", gameId);
            return new GameActionResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<GameActionResult> DiscardCardsAsync(Guid gameId, Guid playerId, IEnumerable<Guid> cardIds)
    {
        try
        {
            var cards = cardIds.ToList();
            if (cards.Count == 0)
                return new GameActionResult(false, "No cards to discard");

            foreach (var cardId in cards)
            {
                await _gameRepository.RemoveCardFromPlayerHandAsync(playerId, cardId);
                await _gameRepository.DiscardCardAsync(gameId, cardId);
            }

            var move = new Move
            {
                GameId = gameId,
                PlayerId = playerId,
                Type = MoveType.Discard,
                Description = $"Discarded {cards.Count} card(s)",
                CardIds = JsonSerializer.Serialize(cards),
                TurnNumber = await _gameRepository.GetNextTurnNumberAsync(gameId),
                Timestamp = DateTime.UtcNow
            };

            await _gameRepository.RecordMoveAsync(move);

            var gameStateDto = await GetGameStateDtoAsync(gameId);

            return new GameActionResult(
                true,
                $"Discarded {cards.Count} card(s)",
                gameStateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discarding cards in game {GameId}", gameId);
            return new GameActionResult(false, $"Error: {ex.Message}");
        }
    }

    // ========== GAME STATE ==========

    public async Task<GameState> GetGameStateAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game == null)
            throw new ArgumentException($"Game {gameId} not found");

        var players = await _gameRepository.GetPlayersInGameAsync(gameId);
        var sleepingQueens = await _gameRepository.GetSleepingQueensAsync(gameId);
        var deckCards = await _gameRepository.GetDeckCardsAsync(gameId);
        var discardPile = await _gameRepository.GetDiscardPileAsync(gameId);
        var recentMoves = await _gameRepository.GetGameMovesAsync(gameId, 10);

        var awakenedQueens = new List<Queen>();
        foreach (var player in players)
        {
            awakenedQueens.AddRange(player.Queens);
        }

        return new GameState
        {
            Game = game,
            Players = players.ToList(),
            SleepingQueens = sleepingQueens.ToList(),
            AwakenedQueens = awakenedQueens,
            Deck = new Deck(deckCards.Select(gc => gc.Card)),
            DiscardPile = discardPile.Select(gc => gc.Card).ToList(),
            RecentMoves = recentMoves.ToList(),
            CurrentPlayer = players.FirstOrDefault(p => p.IsCurrentTurn),
            CurrentPhase = game.Phase
        };
    }

    public async Task<GameStateDto> GetGameStateDtoAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game == null)
            throw new ArgumentException($"Game {gameId} not found");

        var players = await _gameRepository.GetPlayersInGameAsync(gameId);
        var allQueens = await _gameRepository.GetQueensForGameAsync(gameId);
        var deckCards = await _gameRepository.GetDeckCardsAsync(gameId);
        var moves = await _gameRepository.GetGameMovesAsync(gameId, 10);

        return GameStateMapper.ToDto(
            game,
            players.ToList(),
            allQueens.ToList(),
            deckCards.ToList(),
            moves.ToList());  // Pass entity moves
    }

    public async Task<Player> GetCurrentPlayerAsync(Guid gameId)
    {
        var state = await GetGameStateAsync(gameId);
        return state.CurrentPlayer ?? throw new InvalidOperationException("No current player");
    }

    public async Task<bool> IsValidMoveAsync(Guid gameId, Guid playerId, Guid cardId,
        Guid? targetPlayerId = null, Guid? targetQueenId = null)
    {
        var state = await GetGameStateAsync(gameId);
        var game = state.Game;
        var player = state.Players.FirstOrDefault(p => p.Id == playerId);

        if (player == null) return false;

        var card = player.PlayerCards
            .Select(pc => pc.Card)
            .FirstOrDefault(c => c.Id == cardId);

        if (card == null) return false;

        // Check if it's player's turn
        if (state.CurrentPlayer?.Id != playerId)
            return false;

        // Check if card type is enabled in settings
        if (!IsCardTypeEnabled(card.Type, game.Settings))
            return false;

        // Card-specific validation with settings
        return card.Type switch
        {
            CardType.King => game.Settings.EnableSleepingQueens &&
                           CanPlayKing(card, state, player, out _),
            CardType.Knight => game.Settings.AllowQueenStealing &&
                             CanPlayKnight(card, state, player, targetPlayerId ?? Guid.Empty, out _),
            CardType.Dragon => game.Settings.AllowDragonProtection,
            CardType.SleepingPotion => game.Settings.AllowSleepingPotions &&
                                     CanPlayPotion(card, state, player, targetQueenId ?? Guid.Empty, out _),
            CardType.Jester => game.Settings.AllowJester,
            CardType.Number => true,
            _ => false
        };
    }

    public async Task<bool> IsGameOverAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        return game?.Status == GameStatus.Completed || game?.Status == GameStatus.Abandoned;
    }

    public async Task<Player?> CheckForWinnerAsync(Guid gameId)
    {
        var state = await GetGameStateAsync(gameId);

        foreach (var player in state.Players)
        {
            var score = player.Queens.Sum(q => q.PointValue);
            if (score >= state.Game.TargetScore)
            {
                if (state.Game.Settings.RequireExactScoreToWin && score != state.Game.TargetScore)
                    continue;

                return player;
            }
        }

        return null;
    }

    // ========== AI OPERATIONS ==========

    public async Task<GameActionResult> MakeAIMoveAsync(Guid gameId, Guid aiPlayerId)
    {
        var player = await _gameRepository.GetPlayerAsync(aiPlayerId);
        if (player == null || player.Type == PlayerType.Human)
            return new GameActionResult(false, "Not an AI player");

        // Simple AI: just draw a card for now
        // You can replace this with your Silverlight AI logic
        return await DrawCardAsync(gameId, aiPlayerId);
    }

    public async Task ProcessAllAITurnsAsync(Guid gameId)
    {
        var state = await GetGameStateAsync(gameId);
        var aiPlayers = state.Players.Where(p => p.Type != PlayerType.Human).ToList();

        foreach (var aiPlayer in aiPlayers)
        {
            if (state.CurrentPlayer?.Id == aiPlayer.Id)
            {
                await MakeAIMoveAsync(gameId, aiPlayer.Id);
            }
        }
    }

    // ========== GAME SETTINGS ==========

    public async Task UpdateGameSettingsAsync(Guid gameId, GameSettings settings)
    {
        if (!settings.Validate())
            throw new ArgumentException("Invalid game settings");

        await _gameRepository.UpdateGameSettingsAsync(gameId, settings);

        _logger.LogInformation("Game {GameId} settings updated", gameId);
    }

    public async Task<GameSettings> GetGameSettingsAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game == null)
            throw new ArgumentException($"Game {gameId} not found");

        return game.Settings;
    }

    // ========== PRIVATE HELPER METHODS ==========

    private string GenerateGameCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    private bool IsCardTypeEnabled(CardType cardType, GameSettings settings)
    {
        return cardType switch
        {
            CardType.King => settings.KingCardCount > 0,
            CardType.Knight => settings.KnightCardCount > 0 && settings.AllowQueenStealing,
            CardType.Dragon => settings.DragonCardCount > 0 && settings.AllowDragonProtection,
            CardType.SleepingPotion => settings.SleepingPotionCount > 0 && settings.AllowSleepingPotions,
            CardType.Jester => settings.JesterCardCount > 0 && settings.AllowJester,
            CardType.Number => true,
            CardType.Queen => false,
            _ => false
        };
    }

    // ========== CARD HANDLERS ==========

    private async Task<GameActionResult> HandleKingCardAsync(GameState state,
        Player player, Card card, Guid? targetQueenId)
    {
        Queen? queenToWake;

        if (targetQueenId.HasValue)
        {
            queenToWake = state.SleepingQueens.FirstOrDefault(q => q.Id == targetQueenId.Value);
            if (queenToWake == null)
                return new GameActionResult(false, "Target queen not found or already awake");
        }
        else
        {
            queenToWake = state.SleepingQueens.FirstOrDefault();
            if (queenToWake == null)
                return new GameActionResult(false, "No sleeping queens available");
        }

        // Wake the queen
        await _gameRepository.WakeQueenAsync(queenToWake.Id, player.Id);

        // Remove card from player's hand
        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);

        // Add to discard
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var gameStateDto = await GetGameStateDtoAsync(state.Game.Id);
        var gameEvent = new GameEventDto
        {
            Type = GameEventType.QueenWoken,
            Description = $"{player.Name} woke {queenToWake.Name}!",
            Timestamp = DateTime.UtcNow,
            Data = new { Player = player.Name, Queen = queenToWake.Name }
        };

        return new GameActionResult(true, $"{player.Name} woke {queenToWake.Name}!",
            gameStateDto, gameEvent);
    }

    private async Task<GameActionResult> HandleKnightCardAsync(GameState state,
        Player player, Card card, Guid targetPlayerId)
    {
        var targetPlayer = state.Players.FirstOrDefault(p => p.Id == targetPlayerId);
        if (targetPlayer == null)
            return new GameActionResult(false, "Target player not found");

        // Get a random queen from target player
        var queens = targetPlayer.Queens.ToList();
        if (queens.Count == 0)
            return new GameActionResult(false, "Target player has no queens");

        var random = new Random();
        var queenToSteal = queens[random.Next(queens.Count)];

        // Transfer queen
        await _gameRepository.TransferQueenAsync(queenToSteal.Id, player.Id);

        // Remove card from player's hand
        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);

        // Add to discard
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var gameStateDto = await GetGameStateDtoAsync(state.Game.Id);
        var gameEvent = new GameEventDto
        {
            Type = GameEventType.QueenStolen,
            Description = $"{player.Name} stole {queenToSteal.Name} from {targetPlayer.Name}!",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                Attacker = player.Name,
                Defender = targetPlayer.Name,
                Queen = queenToSteal.Name
            }
        };

        return new GameActionResult(true,
            $"{player.Name} stole {queenToSteal.Name} from {targetPlayer.Name}!",
            gameStateDto,
            gameEvent);
    }

    private async Task<GameActionResult> HandleDragonCardAsync(GameState state,
        Player player, Card card, Guid? targetPlayerId)
    {
        // Dragon is usually played defensively
        // For now, just discard it

        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var gameStateDto = await GetGameStateDtoAsync(state.Game.Id);
        var gameEvent = new GameEventDto
        {
            Type = GameEventType.DragonBlocked,
            Description = $"{player.Name} played Dragon for protection",
            Timestamp = DateTime.UtcNow,
            Data = new { Player = player.Name }
        };

        return new GameActionResult(true, $"{player.Name} played Dragon for protection",
            gameStateDto, gameEvent);
    }

    private async Task<GameActionResult> HandlePotionCardAsync(GameState state,
        Player player, Card card, Guid targetQueenId)
    {
        var targetQueen = await _gameRepository.GetQueenByIdAsync(targetQueenId);
        if (targetQueen == null)
            return new GameActionResult(false, "Target queen not found");

        if (!targetQueen.IsAwake)
            return new GameActionResult(false, "Queen is already sleeping");

        // Put queen back to sleep
        await _gameRepository.PutQueenToSleepAsync(targetQueenId);

        // Remove card from player's hand
        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);

        // Add to discard
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var gameStateDto = await GetGameStateDtoAsync(state.Game.Id);
        var gameEvent = new GameEventDto
        {
            Type = GameEventType.PotionUsed,
            Description = $"{player.Name} put {targetQueen.Name} to sleep!",
            Timestamp = DateTime.UtcNow,
            Data = new { Player = player.Name, Queen = targetQueen.Name }
        };

        return new GameActionResult(true,
            $"{player.Name} put {targetQueen.Name} to sleep!",
            gameStateDto,
            gameEvent);
    }

    private async Task<GameActionResult> HandleJesterCardAsync(GameState state,
        Player player, Card card)
    {
        // Jester: Draw a card, if it's a face card, keep it and play again
        var drawnCard = await _gameRepository.DrawCardFromDeckAsync(state.Game.Id);
        if (drawnCard == null)
            return new GameActionResult(false, "Deck is empty");

        await _gameRepository.AddCardToPlayerHandAsync(player.Id, drawnCard.CardId);

        // Remove Jester from hand
        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);

        // Add Jester to discard
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var isFaceCard = drawnCard.Card.Type switch
        {
            CardType.King or CardType.Knight or CardType.Dragon
                or CardType.SleepingPotion or CardType.Jester => true,
            _ => false
        };

        var message = isFaceCard
            ? $"{player.Name} played Jester and drew {drawnCard.Card.Name}! They get another turn!"
            : $"{player.Name} played Jester and drew {drawnCard.Card.Name}. Turn ends.";

        var gameStateDto = await GetGameStateDtoAsync(state.Game.Id);

        // If not face card, end turn automatically
        if (!isFaceCard)
        {
            await EndTurnAsync(state.Game.Id, player.Id);
        }

        return new GameActionResult(true, message, gameStateDto);
    }

    private async Task<GameActionResult> HandleNumberCardAsync(GameState state,
        Player player, Card card)
    {
        // Number cards are usually played in pairs or runs
        // This handler is for single number card play

        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var gameStateDto = await GetGameStateDtoAsync(state.Game.Id);

        return new GameActionResult(true, $"{player.Name} played {card.Name}",
            gameStateDto);
    }

    // ========== VALIDATION METHODS ==========

    private static bool CanPlayKing(Card card, GameState state, Player player, out string? errorMessage)
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

    private bool CanPlayKnight(Card card, GameState state, Player player,
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

        if (!targetPlayer.Queens.Any())
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

    private bool CanPlayPotion(Card card, GameState state, Player player,
        Guid targetQueenId, out string? errorMessage)
    {
        errorMessage = null;

        if (!state.CanPlayCard(card, player))
        {
            errorMessage = "Cannot play this card";
            return false;
        }

        var targetQueen = state.AwakenedQueens.FirstOrDefault(q => q.Id == targetQueenId);
        if (targetQueen == null)
        {
            errorMessage = "Target queen not found";
            return false;
        }

        // Can't put your own queen to sleep (unless house rules allow it)
        if (targetQueen.PlayerId == player.Id && !state.Game.Settings.AllowSelfPotion)
        {
            errorMessage = "Cannot put your own queen to sleep";
            return false;
        }

        return true;
    }

    // Helper method for GameRepository
    private async Task<IEnumerable<Queen>> GetQueensForGameAsync(Guid gameId)
    {
        return await _gameRepository.GetQueensForGameAsync(gameId);
    }
}

// Extension method for GameRepository (add this to GameRepository.cs)
public static class GameRepositoryExtensions
{
    public static async Task<List<Queen>> GetQueensForGameAsync(this IGameRepository repository, Guid gameId)
    {
        // This method should be implemented in GameRepository
        // For now, combine sleeping and player queens
        var sleepingQueens = await repository.GetSleepingQueensAsync(gameId);
        var allQueens = new List<Queen>(sleepingQueens);

        var players = await repository.GetPlayersInGameAsync(gameId);
        foreach (var player in players)
        {
            var playerQueens = await repository.GetPlayerQueensAsync(player.Id);
            allQueens.AddRange(playerQueens);
        }

        return allQueens;
    }
}