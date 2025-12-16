using Microsoft.Extensions.Logging;
using SleepingQueens.GameEngine.AI;
using SleepingQueens.Server.Data.Repositories;
using SleepingQueens.Shared.Models.Game;
using System.Text.Json;

namespace SleepingQueens.GameEngine;

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

        // Record winner if provided
        if (winnerId.HasValue)
        {
            var winner = game.Players.FirstOrDefault(p => p.Id == winnerId.Value);
            if (winner != null)
            {
                // Update winner stats if needed
                _logger.LogInformation("Game {GameId} ended. Winner: {WinnerName}",
                    gameId, winner.Name);
            }
        }

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
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
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
                AILevel.Expert => PlayerType.AI_Hard, // Map Expert to Hard for now
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
        var player = await _gameRepository.GetPlayerAsync(playerId) ?? throw new ArgumentException($"Player {playerId} not found");
        player.Score = score;
        await _gameRepository.UpdateAsync(player.Game); // This will trigger save

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
            GameActionResult result = card.Type switch
            {
                CardType.King => await HandleKingCardAsync(state, player, card, targetQueenId),
                CardType.Knight => await HandleKnightCardAsync(state, player, card, targetPlayerId!.Value),
                CardType.Dragon => await HandleDragonCardAsync(state, player, card, targetPlayerId),
                CardType.SleepingPotion => await HandlePotionCardAsync(state, player, card, targetQueenId!.Value),
                CardType.Jester => await HandleJesterCardAsync(state, player, card),
                CardType.Number => await HandleNumberCardAsync(state, player, card),
                _ => new GameActionResult(false, $"Unhandled card type: {card.Type}")
            };

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
                    result = result with
                    {
                        GameEvent = new GameEvent(
                            GameEventType.GameEnded,
                            $"{winner.Name} wins the game with {winner.Score} points!",
                            Data: winner)
                    };
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing card in game {GameId}", gameId);
            return new GameActionResult(false, $"Error: {ex.Message}");
        }
    }

    public async Task<GameActionResult> DrawCardAsync(Guid gameId, Guid playerId)
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

        var updatedState = await GetGameStateAsync(gameId);

        return new GameActionResult(
            true,
            "Card drawn",
            updatedState,
            new GameEvent(GameEventType.CardPlayed, $"{player.Name} drew a card"));
    }

    public async Task<GameActionResult> EndTurnAsync(Guid gameId, Guid playerId)
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

        var updatedState = await GetGameStateAsync(gameId);

        return new GameActionResult(
            true,
            "Turn ended",
            updatedState,
            new GameEvent(
                GameEventType.TurnEnded,
                $"{players[nextIndex].Name}'s turn now",
                Data: players[nextIndex]));
    }

    public async Task<GameActionResult> DiscardCardsAsync(Guid gameId, Guid playerId, IEnumerable<Guid> cardIds)
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
            Type = MoveType.PlayCard,
            Description = $"Discarded {cards.Count} card(s)",
            CardIds = JsonSerializer.Serialize(cards),
            TurnNumber = await _gameRepository.GetNextTurnNumberAsync(gameId),
            Timestamp = DateTime.UtcNow
        };

        await _gameRepository.RecordMoveAsync(move);

        var updatedState = await GetGameStateAsync(gameId);

        return new GameActionResult(
            true,
            $"Discarded {cards.Count} card(s)",
            updatedState);
    }

    // ========== GAME STATE ==========

    public async Task<GameState> GetGameStateAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
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
            Players = [.. players],
            SleepingQueens = [.. sleepingQueens],
            AwakenedQueens = awakenedQueens,
            Deck = new Deck(deckCards.Select(gc => gc.Card)),
            DiscardPile = [.. discardPile.Select(gc => gc.Card)],
            RecentMoves = [.. recentMoves],
            CurrentPlayer = players.FirstOrDefault(p => p.IsCurrentTurn),
            CurrentPhase = game.Phase
        };
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
            CardType.Number => true, // Always allowed
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
                // If exact score required, check equality
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

        // Get AI logic based on difficulty
        var aiLogic = GetAILogic(player.Type);

        // Decide what move to make
        var decision = await aiLogic.DecideMoveAsync(gameId, aiPlayerId);

        if (decision.Action == AIAction.PlayCard && decision.CardId.HasValue)
        {
            return await PlayCardAsync(
                gameId,
                aiPlayerId,
                decision.CardId.Value,
                decision.TargetPlayerId,
                decision.TargetQueenId);
        }
        else if (decision.Action == AIAction.DrawCard)
        {
            return await DrawCardAsync(gameId, aiPlayerId);
        }
        else if (decision.Action == AIAction.EndTurn)
        {
            return await EndTurnAsync(gameId, aiPlayerId);
        }

        return new GameActionResult(false, "AI couldn't decide on a move");
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
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        return game.Settings;
    }

    // ========== PRIVATE HELPER METHODS ==========

    private static string GenerateGameCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string([.. Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)])]);
    }

    private static bool IsCardTypeEnabled(CardType cardType, GameSettings settings)
    {
        return cardType switch
        {
            CardType.King => settings.KingCardCount > 0,
            CardType.Knight => settings.KnightCardCount > 0 && settings.AllowQueenStealing,
            CardType.Dragon => settings.DragonCardCount > 0 && settings.AllowDragonProtection,
            CardType.SleepingPotion => settings.SleepingPotionCount > 0 && settings.AllowSleepingPotions,
            CardType.Jester => settings.JesterCardCount > 0 && settings.AllowJester,
            CardType.Number => true,
            CardType.Queen => false, // Queens are not played from hand
            _ => false
        };
    }

    private EasyAILogic GetAILogic(PlayerType playerType)
    {
        return playerType switch
        {
            PlayerType.AI_Easy => new EasyAILogic(_gameRepository, _logger),
            PlayerType.AI_Medium => new EasyAILogic(_gameRepository, _logger), // TODO: create MediumAI
            PlayerType.AI_Hard => new EasyAILogic(_gameRepository, _logger), // TODO: create Hard AI
            _ => new EasyAILogic(_gameRepository, _logger)
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

        // Update game state
        var updatedState = await GetGameStateAsync(state.Game.Id);

        return new GameActionResult(
            true,
            $"{player.Name} woke {queenToWake.Name}!",
            updatedState,
            new GameEvent(
                GameEventType.QueenWoken,
                $"{player.Name} woke {queenToWake.Name}",
                Data: new { Player = player.Name, Queen = queenToWake.Name }));
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

        var updatedState = await GetGameStateAsync(state.Game.Id);

        return new GameActionResult(
            true,
            $"{player.Name} stole {queenToSteal.Name} from {targetPlayer.Name}!",
            updatedState,
            new GameEvent(
                GameEventType.QueenStolen,
                $"{player.Name} stole {queenToSteal.Name} from {targetPlayer.Name}",
                Data: new
                {
                    Attacker = player.Name,
                    Defender = targetPlayer.Name,
                    Queen = queenToSteal.Name
                }));
    }

    private async Task<GameActionResult> HandleDragonCardAsync(GameState state,
        Player player, Card card, Guid? targetPlayerId)
    {
        // Dragon is usually played defensively when attacked by Knight
        // For now, just discard it (actual defense logic would be in response to Knight)

        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var updatedState = await GetGameStateAsync(state.Game.Id);

        return new GameActionResult(
            true,
            $"{player.Name} played Dragon for protection",
            updatedState,
            new GameEvent(
                GameEventType.DragonBlocked,
                $"{player.Name} has dragon protection",
                Data: new { Player = player.Name }));
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

        var updatedState = await GetGameStateAsync(state.Game.Id);

        return new GameActionResult(
            true,
            $"{player.Name} put {targetQueen.Name} to sleep!",
            updatedState,
            new GameEvent(
                GameEventType.PotionUsed,
                $"{player.Name} put {targetQueen.Name} to sleep",
                Data: new { Player = player.Name, Queen = targetQueen.Name }));
    }

    private async Task<GameActionResult> HandleJesterCardAsync(GameState state,
        Player player, Card card)
    {
        // Jester: Draw a card, if it's a face card (King, Knight, Dragon, Potion, Jester), 
        // keep it and play again. If number card, turn ends.

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

        var updatedState = await GetGameStateAsync(state.Game.Id);

        // If not face card, end turn automatically
        if (!isFaceCard)
        {
            await EndTurnAsync(state.Game.Id, player.Id);
        }

        return new GameActionResult(
            true,
            message,
            updatedState);
    }

    private async Task<GameActionResult> HandleNumberCardAsync(GameState state,
        Player player, Card card)
    {
        // Number cards are usually played in pairs or runs
        // This handler would be called when playing a single number card
        // For pairs/runs, there would be a separate method

        await _gameRepository.RemoveCardFromPlayerHandAsync(player.Id, card.Id);
        await _gameRepository.DiscardCardAsync(state.Game.Id, card.Id);

        var updatedState = await GetGameStateAsync(state.Game.Id);

        return new GameActionResult(
            true,
            $"{player.Name} played {card.Name}",
            updatedState);
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

    private static bool CanPlayKnight(Card card, GameState state, Player player,
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

    private static bool CanPlayPotion(Card card, GameState state, Player player,
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
}