using Microsoft.EntityFrameworkCore;
using SleepingQueens.Data.Mapping;
using SleepingQueens.Data.Repositories;
using SleepingQueens.Server.Logging;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using System.Text.Json;

namespace SleepingQueens.Server.GameEngine;

public class SleepingQueensGameEngine(
    IGameRepository gameRepository,
    ILogger<SleepingQueensGameEngine> logger) : IGameEngine
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ILogger<SleepingQueensGameEngine> _logger = logger;

    // ========== GAME LIFECYCLE ==========

    public async Task<ActiveGamesResult> GetActiveGamesAsync()
    {
        try
        {
            var games = await _gameRepository.GetActiveGamesAsync();
            var filteredGames = ApplyGameVisibilityRules(games);
            var gameInfos = filteredGames.Select(g => CreateActiveGameInfo(g));

            return ActiveGamesResult.SuccessResult(gameInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetActiveGamesAsync");
            return ActiveGamesResult.Error("Failed to retrieve active games");
        }
    }

    private static IEnumerable<Game> ApplyGameVisibilityRules(IEnumerable<Game> games)
    {
        // Example business rules:
        // - Hide games that have been inactive for too long
        // - Hide private/invite-only games unless user is invited
        // - Hide full games if player can't join

        var cutoffTime = DateTime.UtcNow.AddMinutes(-30); // Hide games inactive for 30+ minutes

        return games.Where(g =>
        {
            // Rule 1: Hide old inactive games
            if (g.Status == GameStatus.Waiting && g.CreatedAt < cutoffTime)
                return false;

            // Rule 2: Hide completed games after some time
            if (g.Status == GameStatus.Completed &&
                g.EndedAt.HasValue &&
                g.EndedAt.Value < DateTime.UtcNow.AddHours(-1))
                return false;

            // Rule 3: Always show active games
            if (g.Status == GameStatus.Active)
                return true;

            // Rule 4: Show waiting games that aren't full (or apply other rules)
            if (g.Status == GameStatus.Waiting && !g.IsFull())
                return true;

            return false;
        });
    }

    private static ActiveGameInfo CreateActiveGameInfo(Game game)
    {
        // Apply any business logic to the info
        var info = new ActiveGameInfo
        {
            GameId = game.Id,
            GameCode = game.Code,
            PlayerCount = game.Players.Count,
            MaxPlayers = game.MaxPlayers,
            Status = game.Status,
            CreatedAt = game.CreatedAt,
            StartedAt = game.StartedAt,
            CanJoin = CanPlayerJoinGame(game),
            TimeRemaining = CalculateTimeRemaining(game),
            GameMode = DetermineGameMode(game.Settings)
        };

        return info;
    }

    private static bool CanPlayerJoinGame(Game game)
    {
        // Business logic: who can join this game?
        return game.Status == GameStatus.Waiting &&
               !game.IsFull() &&
               !game.IsPrivate;
    }

    private static TimeSpan? CalculateTimeRemaining(Game game)
    {
        if (game.Status != GameStatus.Active || !game.StartedAt.HasValue)
            return null;

        // Example: 30-minute game duration
        var gameDuration = TimeSpan.FromMinutes(30);
        var elapsed = DateTime.UtcNow - game.StartedAt.Value;
        var remaining = gameDuration - elapsed;

        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static string DetermineGameMode(GameSettings settings)
    {
        // Business logic to determine game mode
        if (settings.TargetScore <= 20) return "Quick Play";
        if (settings.TargetScore >= 80) return "Marathon";
        return "Standard";
    }

    public async Task<Game> CreateGame(GameSettings settings, Player creator)
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(creator);

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
            // Players is already initialized to [] in Game constructor
        };

        creator.GameId = game.Id;
        creator.IsCurrentTurn = true;
        creator.JoinedAt = DateTime.UtcNow;

        // Add creator to game
        game.Players.Add(creator);

        // Save game
        var savedGame = await _gameRepository.AddAsync(game);

        return savedGame;
    }

    public async Task<Game> StartGameAsync(Guid gameId, Guid requestingPlayerId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        if (game.Players.Count < game.Settings.MinPlayers)
            throw new InvalidOperationException($"Need at least {game.Settings.MinPlayers} players to start");

        if (game.Players.Count > game.Settings.MaxPlayers)
            throw new InvalidOperationException($"Too many players. Maximum is {game.Settings.MaxPlayers}");

        game.Status = GameStatus.Active;
        game.Phase = GamePhase.Playing;
        game.StartedAt = DateTime.UtcNow;

        // Initialize game components
        await InitializeGameComponentsAsync(gameId, game.Settings);

        // Deal initial cards to ALL players
        await DealInitialCardsToAllPlayersAsync(gameId);

        // Set first player's turn
        if (game.Players.Count != 0)
        {
            await _gameRepository.UpdatePlayerTurnAsync(gameId, game.Players.ToArray()[0].Id, true);
        }

        await _gameRepository.UpdateAsync(game);

        _logger.LogGameStarted(gameId, game.Players.Count);

        return game;
    }

    private async Task InitializeGameComponentsAsync(Guid gameId, GameSettings settings)
    {
        await _gameRepository.InitializeDeckAsync(gameId, settings);
        await _gameRepository.PlaceSleepingQueensAsync(gameId, settings);
    }

    private async Task DealInitialCardsToAllPlayersAsync(Guid gameId)
    {
        const int initialHandSize = 5;
        var players = await _gameRepository.GetPlayersInGameAsync(gameId);

        foreach (var player in players)
        {
            for (int i = 0; i < initialHandSize; i++)
            {
                var drawnCard = await _gameRepository.DrawCardFromDeckAsync(gameId);
                if (drawnCard != null)
                {
                    await _gameRepository.AddCardToPlayerHandAsync(player.Id, drawnCard.CardId);
                }
            }

            _logger.LogPlayerDealtCards(player.Id, initialHandSize);
        }
    }

    public async Task<Game> EndGameAsync(Guid gameId, Guid? winnerId = null)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        game.Status = GameStatus.Completed;
        game.Phase = GamePhase.Ended;
        game.EndedAt = DateTime.UtcNow;

        await _gameRepository.UpdateAsync(game);

        _logger.LogGameEnded(gameId);

        return game;
    }

    public async Task<Game> AbandonGameAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        game.Status = GameStatus.Abandoned;
        game.Phase = GamePhase.Ended;
        game.EndedAt = DateTime.UtcNow;

        await _gameRepository.UpdateAsync(game);

        _logger.LogGameAbandoned(gameId);

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

        _logger.LogPlayerJoined(player.Name, gameId);

        return addedPlayer;
    }

    public async Task<AddAIPlayerResult> AddAIPlayerAsync(Guid gameId, AILevel level, Guid requestingPlayerId)
    {
        try
        {
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (game == null)
                return AddAIPlayerResult.Fail("Game not found");

            // Validate requesting player is host
            if (!IsPlayerHost(game, requestingPlayerId))
                return AddAIPlayerResult.Fail("Only the host can add AI players");

            if (game.IsFull())
                return AddAIPlayerResult.Fail("Game is full");

            var player = new Player
            {
                Name = $"AI ({level})",
                Type = level switch
                {
                    AILevel.Easy => PlayerType.AI_Easy,
                    AILevel.Medium => PlayerType.AI_Medium,
                    AILevel.Hard => PlayerType.AI_Hard,
                    _ => PlayerType.AI_Medium
                },
                IsAI = true,
                AILevel = level
            };

            var addedPlayer = await _gameRepository.AddPlayerAsync(gameId, player);

            // If game is already active, deal cards to this AI player
            if (game.Status == GameStatus.Active)
            {
                await DealInitialCardsToPlayerAsync(gameId, addedPlayer.Id);
            }

            return AddAIPlayerResult.Ok(
                addedPlayer.Id,
                addedPlayer.Name,
                game.Players.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding AI player");
            return AddAIPlayerResult.Fail($"Error: {ex.Message}");
        }
    }

    private static bool IsPlayerHost(Game game, Guid playerId)
    {
        var host = game.Players.OrderBy(p => p.JoinedAt).FirstOrDefault();
        return host?.Id == playerId;
    }

    private async Task DealInitialCardsToPlayerAsync(Guid gameId, Guid playerId)
    {
        const int initialHandSize = 5;

        for (int i = 0; i < initialHandSize; i++)
        {
            var drawnCard = await _gameRepository.DrawCardFromDeckAsync(gameId);
            if (drawnCard != null)
            {
                await _gameRepository.AddCardToPlayerHandAsync(playerId, drawnCard.CardId);
            }
        }
    }

    public async Task<RemovePlayerResult> RemovePlayerAsync(Guid gameId, Guid playerIdToRemove, Guid requestingPlayerId)
    {
        try
        {
            // 1. Get data from repository
            var game = await _gameRepository.GetByIdAsync(gameId);
            if (game == null)
                return RemovePlayerResult.Error("Game not found");

            // 2. Business validation
            var validationResult = ValidatePlayerRemoval(
                game,
                playerIdToRemove,
                requestingPlayerId);

            if (!validationResult.IsValid)
                return RemovePlayerResult.Error(validationResult.ErrorMessage ?? "");

            // 3. Business logic: Remove player from game
            var removedPlayer = await RemovePlayerFromGameAsync(gameId, playerIdToRemove);

            // 4. Additional business rules
            await HandlePostRemovalLogicAsync(gameId, playerIdToRemove);

            return RemovePlayerResult.SuccessResult(removedPlayer.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing player");
            return RemovePlayerResult.Error($"Error: {ex.Message}");
        }
    }

    private static ValidationResult ValidatePlayerRemoval(
        Game game,
        Guid playerIdToRemove,
        Guid requestingPlayerId)
    {
        // Find requesting player
        var requester = game.Players.FirstOrDefault(p => p.Id == requestingPlayerId);
        if (requester == null)
            return ValidationResult.Invalid("You are not in this game");

        // Check if requester is host
        var host = game.Players.OrderBy(p => p.JoinedAt).First();
        if (host.Id != requestingPlayerId)
            return ValidationResult.Invalid("Only the host can remove players");

        // Find player to remove
        var playerToRemove = game.Players.FirstOrDefault(p => p.Id == playerIdToRemove);
        if (playerToRemove == null)
            return ValidationResult.Invalid("Player not found");

        // Can't remove host
        if (playerToRemove.Id == host.Id)
            return ValidationResult.Invalid("Cannot remove the host");

        // Additional business rules
        if (game.Status == GameStatus.Active && game.Players.Count <= 2)
            return ValidationResult.Invalid("Cannot remove player during active game with only 2 players");

        return ValidationResult.Valid();
    }

    private async Task<Player> RemovePlayerFromGameAsync(Guid gameId, Guid playerId)
    {
        // This could involve multiple repository calls
        var player = await _gameRepository.GetPlayerAsync(playerId) ?? throw new InvalidOperationException("Player not found");

        // Remove player's cards from hand
        var playerHand = await _gameRepository.GetPlayerHandAsync(playerId);
        foreach (var card in playerHand)
        {
            await _gameRepository.DiscardCardAsync(gameId, card.Id);
        }

        // Return player's queens to sleeping pool
        var playerQueens = await _gameRepository.GetPlayerQueensAsync(playerId);
        foreach (var queen in playerQueens)
        {
            await _gameRepository.PutQueenToSleepAsync(queen.Id);
        }

        // Finally remove player
        await _gameRepository.RemovePlayerAsync(playerId);

        return player;
    }

    private async Task HandlePostRemovalLogicAsync(Guid gameId, Guid removedPlayerId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);

        // If game is active and removed player was current turn, move to next player
        if (game?.Status == GameStatus.Active)
        {
            var removedPlayer = game.Players.FirstOrDefault(p => p.Id == removedPlayerId);
            if (removedPlayer?.IsCurrentTurn == true)
            {
                //await HandleTurnAfterPlayerRemovalAsync(gameId);
            }
        }
    }

    public async Task UpdatePlayerScoreAsync(Guid playerId, int score)
    {
        var player = await _gameRepository.GetPlayerAsync(playerId) ?? throw new ArgumentException($"Player {playerId} not found");
        player.Score = score;
        await _gameRepository.UpdateAsync(player.Game);

        _logger.LogPlayerScoreUpdate(playerId, score);
    }

    public async Task<JoinGameResult> JoinGameAsync(string gameCode, string playerName, string connectionId, Guid? existingPlayerId = null)
    {
        try
        {
            // 1. Get game data
            var game = await _gameRepository.GetByCodeAsync(gameCode);
            if (game == null)
                return JoinGameResult.Error("Game not found");

            // 2. Business validation
            var validationResult = ValidateGameJoin(game, playerName);
            if (!validationResult.IsValid)
                return JoinGameResult.Error(validationResult.ErrorMessage ?? "");

            // 3. Handle reconnection if existing player
            Player addedPlayer;
            if (existingPlayerId.HasValue)
            {
                addedPlayer = await HandlePlayerReconnectionAsync(game.Id, existingPlayerId.Value, connectionId);
            }
            else
            {
                // 4. Create new player
                var player = new Player
                {
                    Name = playerName,
                    ConnectionId = connectionId,
                    GameId = game.Id,
                    Type = PlayerType.Human
                };

                addedPlayer = await _gameRepository.AddPlayerAsync(game.Id, player);
            }

            // 5. Get updated game state
            var gameStateDto = await GetGameStateDtoAsync(game.Id);

            return JoinGameResult.SuccessResult(
                game.Id,
                addedPlayer.Id,
                addedPlayer.Name,
                game.Players.Count,
                gameStateDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining game");
            return JoinGameResult.Error($"Error: {ex.Message}");
        }
    }

    private static ValidationResult ValidateGameJoin(Game game, string playerName)
    {
        // Name validation
        if (string.IsNullOrWhiteSpace(playerName))
            return ValidationResult.Invalid("Player name is required");

        if (playerName.Length > 50)
            return ValidationResult.Invalid("Player name too long (max 50 characters)");

        // Game state validation
        if (game.IsFull())
            return ValidationResult.Invalid("Game is full");

        if (game.Status != GameStatus.Waiting)
            return ValidationResult.Invalid("Game already started");

        // Check for duplicate names (optional)
        var existingPlayer = game.Players.FirstOrDefault(p =>
            p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

        if (existingPlayer != null)
            return ValidationResult.Invalid($"Name '{playerName}' is already taken");

        return ValidationResult.Valid();
    }

    private async Task<Player> HandlePlayerReconnectionAsync(Guid gameId, Guid playerId, string newConnectionId)
    {
        var player = await _gameRepository.GetPlayerAsync(playerId) ?? throw new InvalidOperationException("Player not found");

        // Update connection ID
        player.ConnectionId = newConnectionId;
        await _gameRepository.UpdateAsync(player.Game);

        _logger.LogPlayerReconnected(playerId, gameId);

        return player;
    }

    public async Task<PlayerDisconnectResult> HandlePlayerDisconnectAsync(Guid playerId)
    {
        try
        {
            // 1. Get player and game data
            var player = await _gameRepository.GetPlayerAsync(playerId);
            if (player == null)
                return PlayerDisconnectResult.PlayerNotFound();

            var game = await _gameRepository.GetByIdAsync(player.GameId);
            if (game == null)
                return PlayerDisconnectResult.GameNotFound();

            // 2. Determine disconnect handling based on game state
            return game.Status switch
            {
                GameStatus.Waiting => await HandleLobbyDisconnectAsync(game, player),
                GameStatus.Active => await HandleActiveGameDisconnectAsync(game, player),
                _ => PlayerDisconnectResult.NoActionNeeded(game.Id, player.Name)
            };
        }
        catch (Exception ex)
        {
            _logger.LogPlayerDisconnectError(ex, playerId);
            return PlayerDisconnectResult.Error($"Error handling disconnect: {ex.Message}");
        }
    }

    private async Task<PlayerDisconnectResult> HandleLobbyDisconnectAsync(Game game, Player player)
    {
        // In lobby: simply remove the player
        await _gameRepository.RemovePlayerAsync(player.Id);

        // Check if host left - need to assign new host
        var host = game.Players.OrderBy(p => p.JoinedAt).FirstOrDefault();
        bool wasHost = host?.Id == player.Id;

        if (wasHost && game.Players.Count > 1)
        {
            var newHost = game.Players
                .Where(p => p.Id != player.Id)
                .OrderBy(p => p.JoinedAt)
                .FirstOrDefault();

            if (newHost != null)
            {
                // Could mark new host in some way if needed
                _logger.LogHostChanged(player.Id, newHost.Id, game.Id);
            }
        }

        return PlayerDisconnectResult.PlayerRemoved(
            game.Id,
            player.Name,
            wasHost,
            "Player left the lobby");
    }

    private async Task<PlayerDisconnectResult> HandleActiveGameDisconnectAsync(Game game, Player player)
    {
        // Mark player as disconnected (not removed)
        player.IsConnected = false;
        player.LastDisconnectedAt = DateTime.UtcNow;
        await _gameRepository.UpdateAsync(game);

        // Check if we should end the game
        var activePlayers = game.Players.Count(p => p.IsConnected);
        bool shouldEndGame = activePlayers < game.Settings.MinPlayers;

        if (shouldEndGame)
        {
            // End the game due to insufficient players
            await EndGameAsync(game.Id, null); // No winner
            return PlayerDisconnectResult.GameEnded(
                game.Id,
                player.Name,
                "Game ended due to player disconnect");
        }

        // Handle turn if disconnected player was current player
        if (player.IsCurrentTurn)
        {
            await HandleTurnAfterDisconnectAsync(game.Id, player.Id);
        }

        return PlayerDisconnectResult.PlayerDisconnected(
            game.Id,
            player.Name,
            true, // can reconnect
            "Player disconnected - can reconnect within 2 minutes");
    }

    private async Task HandleTurnAfterDisconnectAsync(Guid gameId, Guid disconnectedPlayerId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId);
        if (game == null) return;

        var players = game.Players
            .Where(p => p.IsConnected)
            .OrderBy(p => p.JoinedAt)
            .ToList();

        if (players.Count == 0) return;

        // Find next connected player
        var currentIndex = players.FindIndex(p => p.Id == disconnectedPlayerId);
        int nextIndex = currentIndex >= 0
            ? (currentIndex + 1) % players.Count
            : 0;

        // Update turns
        await _gameRepository.UpdatePlayerTurnAsync(gameId, disconnectedPlayerId, false);
        await _gameRepository.UpdatePlayerTurnAsync(gameId, players[nextIndex].Id, true);

        _logger.LogTurnSkippedDueToDisconnect(disconnectedPlayerId, players[nextIndex].Id, gameId);
    }

    public async Task<ReconnectPlayerResult> ReconnectPlayerAsync(Guid gameId, Guid playerId, string connectionId)
    {
        try
        {
            // 1. Get player and game data
            var player = await _gameRepository.GetPlayerAsync(playerId);
            if (player == null)
                return ReconnectPlayerResult.Error("Player not found");

            if (player.GameId != gameId)
                return ReconnectPlayerResult.Error("Player is not in this game");

            var game = await _gameRepository.GetByIdAsync(gameId);
            if (game == null)
                return ReconnectPlayerResult.Error("Game not found");

            // 2. Validate reconnection is allowed
            var validationResult = ValidateReconnection(game, player);
            if (!validationResult.IsValid)
                return ReconnectPlayerResult.Error(validationResult.ErrorMessage!);

            // 3. Update player connection
            player.ConnectionId = connectionId;
            player.IsConnected = true;
            player.LastReconnectedAt = DateTime.UtcNow;

            // 4. Handle special cases
            await HandleReconnectionSpecialCasesAsync(game, player);

            // 5. Save changes
            await _gameRepository.UpdateAsync(game);

            _logger.LogPlayerReconnected(playerId, gameId);

            return ReconnectPlayerResult.SuccessResult(player.Name);
        }
        catch (Exception ex)
        {
            _logger.LogPlayerReconnectError(ex, playerId, gameId);
            return ReconnectPlayerResult.Error($"Reconnection failed: {ex.Message}");
        }
    }

    private static ValidationResult ValidateReconnection(Game game, Player player)
    {
        // Game must be active
        if (game.Status != GameStatus.Active)
            return ValidationResult.Invalid("Cannot reconnect to a game that is not active");

        // Player must be part of the game
        if (!game.Players.Any(p => p.Id == player.Id))
            return ValidationResult.Invalid("Player is not part of this game");

        // Check if player was marked as disconnected
        if (player.IsConnected)
            return ValidationResult.Invalid("Player is already connected");

        // Check reconnection window (optional - you could remove this if GameHub handles it)
        if (player.LastDisconnectedAt.HasValue)
        {
            var timeSinceDisconnect = DateTime.UtcNow - player.LastDisconnectedAt.Value;
            if (timeSinceDisconnect.TotalMinutes > 5) // 5-minute reconnection window
                return ValidationResult.Invalid("Reconnection window has expired");
        }

        return ValidationResult.Valid();
    }

    private async Task HandleReconnectionSpecialCasesAsync(Game game, Player player)
    {
        // If this is the only reconnected player and game was paused, resume game
        var disconnectedPlayers = game.Players.Count(p => !p.IsConnected);
        var totalPlayers = game.Players.Count;

        if (disconnectedPlayers == 0)
        {
            // All players are now connected
            _logger.LogAllPlayersReconnected(game.Id);

            // You could send a notification that game is fully reconnected
            // await _notificationService.NotifyGameResumedAsync(game.Id);
        }

        // If player was AI-controlled while disconnected, remove AI control
        if (player.IsAIControlled)
        {
            player.IsAIControlled = false;
            _logger.LogPlayerRegainedControl(player.Id, game.Id);
        }
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
            _logger.LogGamePlayCardError(ex, gameId);
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
            CardType.Dragon => await HandleDragonCardAsync(state, player, card),
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
            _logger.LogGameDrawCardError(ex, gameId);
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
            _logger.LogGameEndTurnError(ex, gameId);
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
            _logger.LogGameDiscardingError(ex, gameId);
            return new GameActionResult(false, $"Error: {ex.Message}");
        }
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

    public async Task<GameStateDto> GetGameStateDtoAsync(Guid gameId)
    {
        var game = await _gameRepository.GetByIdAsync(gameId) ?? throw new ArgumentException($"Game {gameId} not found");
        var players = await _gameRepository.GetPlayersInGameAsync(gameId);
        var allQueens = await _gameRepository.GetQueensForGameAsync(gameId);
        var deckCards = await _gameRepository.GetDeckCardsAsync(gameId);
        var moves = await _gameRepository.GetGameMovesAsync(gameId, 10);

        return GameStateMapper.ToDto(
            game,
            [.. players],
            [.. allQueens],
            [.. deckCards],
            [.. moves]);  // Pass entity moves
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

        _logger.LogSettingsUpdated(gameId);
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
        Player player, Card card)
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