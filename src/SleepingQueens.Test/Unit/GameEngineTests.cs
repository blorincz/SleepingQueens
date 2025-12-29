using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SleepingQueens.Data.Repositories;
using SleepingQueens.Server.GameEngine;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using SleepingQueens.Tests.Helpers;
using System.Text.Json;
using Xunit;

namespace SleepingQueens.Tests.Unit;

public class GameEngineTests
{
    private readonly Mock<IGameRepository> _mockGameRepository;
    private readonly Mock<ILogger<SleepingQueensGameEngine>> _mockLogger;
    private readonly SleepingQueensGameEngine _gameEngine;

    public GameEngineTests()
    {
        _mockGameRepository = new Mock<IGameRepository>();
        _mockLogger = new Mock<ILogger<SleepingQueensGameEngine>>();

        _gameEngine = new SleepingQueensGameEngine(
            _mockGameRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void CreateGame_ValidInput_CreatesGameWithPlayer()
    {
        // Arrange
        var settings = GameSettings.Default;
        var creator = TestDataGenerator.CreateTestPlayer();

        // Act
        var game = _gameEngine.CreateGame(settings, creator);

        // Assert
        game.Should().NotBeNull();
        game.Code.Should().NotBeNullOrEmpty();
        game.Code.Length.Should().Be(6);
        game.Status.Should().Be(GameStatus.Waiting);

        // Compare only the properties that matter for this test
        game.Settings.MaxPlayers.Should().Be(settings.MaxPlayers);
        game.Settings.TargetScore.Should().Be(settings.TargetScore);
        game.Settings.StartingHandSize.Should().Be(settings.StartingHandSize);

        game.Players.Should().ContainSingle();
        game.Players.First().Id.Should().Be(creator.Id);
        game.Players.First().IsCurrentTurn.Should().BeTrue();
    }

    [Fact]
    public void CreateGame_NullSettings_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var creator = TestDataGenerator.CreateTestPlayer();
        var action = () => _gameEngine.CreateGame(null!, creator);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public void CreateGame_NullCreator_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = GameSettings.Default;

        // Act
        var action = () => _gameEngine.CreateGame(settings, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("creator");
    }

    [Fact]
    public async Task StartGameAsync_ValidGame_StartsGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Create players first
        var player1 = RepositoryMockHelper.CreateTestPlayerWithCollections(
            gameId: gameId,
            name: "Player1",
            isCurrentTurn: true);

        var player2 = RepositoryMockHelper.CreateTestPlayerWithCollections(
            gameId: gameId,
            name: "Player2",
            isCurrentTurn: false);

        var players = new List<Player> { player1, player2 };

        // Create game WITH players
        var game = RepositoryMockHelper.CreateTestGameWithCollections(
            id: gameId,
            status: GameStatus.Waiting,
            players: players);

        // Setup mocks
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(game);

        _mockGameRepository.Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        // Act
        var startedGame = await _gameEngine.StartGameAsync(gameId);

        // Assert
        startedGame.Should().NotBeNull();
        startedGame.Status.Should().Be(GameStatus.Active);
        startedGame.Phase.Should().Be(GamePhase.Playing);
        startedGame.StartedAt.Should().NotBeNull();
        startedGame.Players.Should().HaveCount(2); // Verify players are there
    }

    [Fact]
    public async Task StartGameAsync_GameNotFound_ThrowsArgumentException()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync((Game?)null);

        // Act
        var exception = await Record.ExceptionAsync(() => _gameEngine.StartGameAsync(gameId));

        // Assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<ArgumentException>();
        exception.Message.Should().Contain($"Game {gameId} not found");
    }

    [Fact]
    public async Task PlayCardAsync_KingCard_WakesQueen()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var kingCardId = Guid.NewGuid();
        var queenId = Guid.NewGuid();

        // Create game
        var game = RepositoryMockHelper.CreateTestGameWithCollections(
            id: gameId,
            status: GameStatus.Active);

        // Create player with king card in hand
        var player = RepositoryMockHelper.CreateTestPlayerWithCollections(
            id: playerId,
            gameId: gameId,
            name: "Player1",
            isCurrentTurn: true);

        var kingCard = new Card
        {
            Id = kingCardId,
            Type = CardType.King,
            Name = "King",
            Value = 0
        };

        player.PlayerCards = new List<PlayerCard>
    {
        new PlayerCard
        {
            Id = Guid.NewGuid(),
            Card = kingCard,
            CardId = kingCardId,
            PlayerId = playerId,
            HandPosition = 0
        }
    };

        // Create sleeping queen
        var sleepingQueen = new Queen
        {
            Id = queenId,
            Type = QueenType.RoseQueen,
            Name = "Rose Queen",
            PointValue = 5,
            IsAwake = false,
            GameId = gameId,
            PlayerId = null, // Not owned by anyone yet
            ImagePath = "/images/queens/rose.png"
        };

        var players = new List<Player> { player };
        var sleepingQueens = new List<Queen> { sleepingQueen };

        // Setup ALL required mocks for GetGameStateAsync
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(game);

        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId))
            .ReturnsAsync(players);

        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId))
            .ReturnsAsync(sleepingQueens);

        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId))
            .ReturnsAsync(new List<GameCard>());

        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId))
            .ReturnsAsync(new List<GameCard>());

        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10))
            .ReturnsAsync(new List<Move>());

        // Setup mocks for HandleKingCardAsync operations
        _mockGameRepository.Setup(r => r.GetQueenByIdAsync(queenId))
            .ReturnsAsync(sleepingQueen);

        _mockGameRepository.Setup(r => r.WakeQueenAsync(queenId, playerId))
            .Returns(Task.CompletedTask);

        _mockGameRepository.Setup(r => r.RemoveCardFromPlayerHandAsync(playerId, kingCardId))
            .Returns(Task.CompletedTask);

        _mockGameRepository.Setup(r => r.DiscardCardAsync(gameId, kingCardId))
            .Returns(Task.CompletedTask);

        _mockGameRepository.Setup(r => r.RecordMoveAsync(It.IsAny<Move>()))
            .ReturnsAsync(new Move { Id = Guid.NewGuid() });

        _mockGameRepository.Setup(r => r.GetNextTurnNumberAsync(gameId))
            .ReturnsAsync(1);

        _mockGameRepository.Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _gameEngine.PlayCardAsync(
            gameId, playerId, kingCardId, null, queenId);

        // Debug output
        Console.WriteLine($"Result Success: {result.Success}");
        Console.WriteLine($"Result Message: {result.Message}");

        // If result.Success is false, check what the error message is
        if (!result.Success)
        {
            Console.WriteLine($"Error: {result.Message}");
        }

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(because: $"PlayCardAsync failed with message: {result.Message}");
        result.Message.Should().Contain("woke", because: "King card should wake a queen");
    }

    [Fact]
    public async Task DrawCardAsync_ValidRequest_DrawsCard()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        // Minimal setup
        var game = new Game { Id = gameId, Status = GameStatus.Active, Phase = GamePhase.Playing };
        var player = new Player { Id = playerId, IsCurrentTurn = true, GameId = gameId };
        game.Players = new List<Player> { player };

        var drawnCard = new GameCard
        {
            Id = Guid.NewGuid(),
            Card = new Card { Id = Guid.NewGuid(), Type = CardType.Number, Name = "5", Value = 5 }
        };

        // Mock essentials
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);
        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId)).ReturnsAsync(new List<Player> { player });
        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId)).ReturnsAsync(new List<Queen>());
        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10)).ReturnsAsync(new List<Move>());

        _mockGameRepository.Setup(r => r.DrawCardFromDeckAsync(gameId)).ReturnsAsync(drawnCard);
        _mockGameRepository.Setup(r => r.AddCardToPlayerHandAsync(playerId, It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _mockGameRepository.Setup(r => r.RecordMoveAsync(It.IsAny<Move>())).ReturnsAsync(new Move());
        _mockGameRepository.Setup(r => r.GetNextTurnNumberAsync(gameId)).ReturnsAsync(1);

        // Act
        var result = await _gameEngine.DrawCardAsync(gameId, playerId);

        // Assert
        Console.WriteLine($"Result: Success={result.Success}, Message={result.Message}");
        result.Success.Should().BeTrue($"because: {result.Message}");
    }

    [Fact]
    public async Task EndTurnAsync_ValidTurn_AdvancesToNextPlayer()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();

        // Minimal game
        var game = new Game
        {
            Id = gameId,
            Status = GameStatus.Active,
            Phase = GamePhase.Playing
        };

        // Players with proper JoinedAt ordering
        var player1 = new Player
        {
            Id = player1Id,
            Name = "Player1",
            IsCurrentTurn = true,
            GameId = gameId,
            JoinedAt = DateTime.UtcNow.AddMinutes(-10) // Earlier
        };

        var player2 = new Player
        {
            Id = player2Id,
            Name = "Player2",
            IsCurrentTurn = false,
            GameId = gameId,
            JoinedAt = DateTime.UtcNow.AddMinutes(-5) // Later
        };

        game.Players = new List<Player> { player1, player2 };

        // Mock essentials for GetGameStateAsync
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);
        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId)).ReturnsAsync(new List<Player> { player1, player2 });
        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId)).ReturnsAsync(new List<Queen>());
        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10)).ReturnsAsync(new List<Move>());

        // Mock EndTurnAsync operations
        _mockGameRepository.Setup(r => r.UpdatePlayerTurnAsync(gameId, player1Id, false)).Returns(Task.CompletedTask);
        _mockGameRepository.Setup(r => r.UpdatePlayerTurnAsync(gameId, player2Id, true)).Returns(Task.CompletedTask);
        _mockGameRepository.Setup(r => r.RecordMoveAsync(It.IsAny<Move>())).ReturnsAsync(new Move());
        _mockGameRepository.Setup(r => r.GetNextTurnNumberAsync(gameId)).ReturnsAsync(1);

        // Mock GetGameStateDtoAsync (called at the end)
        _mockGameRepository.Setup(r => r.GetGameStateDtoAsync(gameId))
            .ReturnsAsync(new GameStateDto {
                Game = new GameDto { Id = gameId },
                Players = new List<PlayerDto>(),
                SleepingQueens = new List<QueenDto>(),
                AwakenedQueens = new List<QueenDto>(),
                DeckCards = new List<CardDto>(),
                DiscardPile = new List<CardDto>(),
                RecentMoves = new List<MoveDto>(),
                CurrentPlayer = null,
                CurrentPhase = GamePhase.Playing
            });

        // Act
        var result = await _gameEngine.EndTurnAsync(gameId, player1Id);

        // Assert
        Console.WriteLine($"Result: Success={result.Success}, Message={result.Message}");
        result.Success.Should().BeTrue($"because: {result.Message}");
    }

    [Fact]
    public async Task GetGameStateDtoAsync_ValidGame_ReturnsState()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = RepositoryMockHelper.CreateTestGameWithCollections(id: gameId, status: GameStatus.Active);
        var player = RepositoryMockHelper.CreateTestPlayerWithCollections(gameId: gameId, isCurrentTurn: true);
        var players = new List<Player> { player };

        var queen = RepositoryMockHelper.CreateTestQueen(
            playerId: player.Id,
            gameId: gameId,
            isAwake: true);
        var queens = new List<Queen> { queen };

        // Setup all mocks using helper
        _mockGameRepository.SetupGetGameStateDtoMocks(gameId, game, players, queens);

        // Act
        var result = await _gameEngine.GetGameStateDtoAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.Game.Id.Should().Be(gameId);
        result.Players.Should().HaveCount(1);
        result.Players[0].Id.Should().Be(player.Id);
        result.AwakenedQueens.Should().HaveCount(1);
        result.AwakenedQueens[0].PlayerId.Should().Be(player.Id);
    }

    [Fact]
    public async Task PlayCardAsync_CardNotInPlayerHand_ReturnsFailure()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var invalidCardId = Guid.NewGuid();

        // Setup minimal valid game state
        var game = new Game
        {
            Id = gameId,
            Status = GameStatus.Active,
            Phase = GamePhase.Playing
        };

        var player = new Player
        {
            Id = playerId,
            IsCurrentTurn = true,
            GameId = gameId,
            PlayerCards = new List<PlayerCard>(), // Empty hand
            Queens = new List<Queen>()
        };

        game.Players = new List<Player> { player };

        // Mock GetGameStateAsync
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);
        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId)).ReturnsAsync(new List<Player> { player });
        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId)).ReturnsAsync(new List<Queen>());
        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10)).ReturnsAsync(new List<Move>());

        // Act
        var result = await _gameEngine.PlayCardAsync(gameId, playerId, invalidCardId, null, null);

        // Assert
        result.Success.Should().BeFalse("because card is not in player's hand");
        // Don't check specific message, just that it's a failure
    }

    [Fact]
    public async Task PlayCardAsync_KnightCard_StealsQueen()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var knightCardId = Guid.NewGuid();
        var queenId = Guid.NewGuid();

        // Minimal setup
        var game = new Game
        {
            Id = gameId,
            Status = GameStatus.Active,
            Phase = GamePhase.Playing
        };

        var player1 = new Player
        {
            Id = player1Id,
            Name = "Player1",
            IsCurrentTurn = true, // CRITICAL!
            GameId = gameId,
            JoinedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var knightCard = new Card { Id = knightCardId, Type = CardType.Knight, Name = "Knight" };
        player1.PlayerCards = new List<PlayerCard>
    {
        new PlayerCard { Card = knightCard, CardId = knightCardId, PlayerId = player1Id }
    };

        var player2 = new Player
        {
            Id = player2Id,
            Name = "Player2",
            IsCurrentTurn = false,
            GameId = gameId,
            JoinedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var queen = new Queen
        {
            Id = queenId,
            Type = QueenType.RoseQueen,
            IsAwake = true,
            PlayerId = player2Id, // Owned by player2
            GameId = gameId
        };
        player2.Queens = new List<Queen> { queen };

        game.Players = new List<Player> { player1, player2 };

        // Mock GetGameStateAsync
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);
        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId)).ReturnsAsync(game.Players);
        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId)).ReturnsAsync(new List<Queen>());
        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10)).ReturnsAsync(new List<Move>());

        // Mock Knight operations
        _mockGameRepository.Setup(r => r.GetQueenByIdAsync(queenId)).ReturnsAsync(queen);
        _mockGameRepository.Setup(r => r.TransferQueenAsync(queenId, player1Id)).Returns(Task.CompletedTask);
        _mockGameRepository.Setup(r => r.RemoveCardFromPlayerHandAsync(player1Id, knightCardId)).Returns(Task.CompletedTask);
        _mockGameRepository.Setup(r => r.DiscardCardAsync(gameId, knightCardId)).Returns(Task.CompletedTask);
        _mockGameRepository.Setup(r => r.RecordMoveAsync(It.IsAny<Move>())).ReturnsAsync(new Move());
        _mockGameRepository.Setup(r => r.GetNextTurnNumberAsync(gameId)).ReturnsAsync(1);
        _mockGameRepository.Setup(r => r.UpdateAsync(It.IsAny<Game>())).Returns(Task.CompletedTask);

        // Mock GetGameStateDtoAsync
        _mockGameRepository.Setup(r => r.GetGameStateDtoAsync(gameId))
            .ReturnsAsync(new GameStateDto {
                Game = new GameDto { Id = gameId },
                Players = new List<PlayerDto>(),
                SleepingQueens = new List<QueenDto>(),
                AwakenedQueens = new List<QueenDto>(),
                DeckCards = new List<CardDto>(),
                DiscardPile = new List<CardDto>(),
                RecentMoves = new List<MoveDto>(),
                CurrentPlayer = null,
                CurrentPhase = GamePhase.Playing
            });

        // Act
        var result = await _gameEngine.PlayCardAsync(gameId, player1Id, knightCardId, player2Id, queenId);

        // Assert
        Console.WriteLine($"Result: Success={result.Success}, Message={result.Message}");
        result.Success.Should().BeTrue($"because: {result.Message}");
    }

    [Fact]
    public async Task CheckForWinner_NoWinner_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var game = new Game
        {
            Id = gameId,
            Status = GameStatus.Active,
            TargetScore = 40
        };

        var player = new Player
        {
            Id = playerId,
            Name = "Player",
            GameId = gameId,
            Score = 30 // Below target
        };

        player.Queens = new List<Queen>
    {
        new Queen { PointValue = 15, IsAwake = true, PlayerId = playerId, GameId = gameId },
        new Queen { PointValue = 10, IsAwake = true, PlayerId = playerId, GameId = gameId },
        new Queen { PointValue = 5, IsAwake = true, PlayerId = playerId, GameId = gameId }
    };

        game.Players = new List<Player> { player };

        // Mock GetGameStateAsync
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);
        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId)).ReturnsAsync(new List<Player> { player });
        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId)).ReturnsAsync(new List<Queen>());
        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10)).ReturnsAsync(new List<Move>());

        // Act
        var winner = await _gameEngine.CheckForWinnerAsync(gameId);

        // Assert
        winner.Should().BeNull();
    }

    [Fact]
    public async Task CheckForWinner_HasWinner_ReturnsWinnerId()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        // Simple setup
        var game = new Game
        {
            Id = gameId,
            Status = GameStatus.Active,
            TargetScore = 40,
            SettingsJson = JsonSerializer.Serialize(GameSettings.Default)
        };

        var player = new Player
        {
            Id = playerId,
            Name = "Winner",
            GameId = gameId,
            Score = 50 // Above target
        };

        // Add queens worth 50 points
        player.Queens = new List<Queen>
    {
        new Queen { PointValue = 20, IsAwake = true, PlayerId = playerId, GameId = gameId },
        new Queen { PointValue = 15, IsAwake = true, PlayerId = playerId, GameId = gameId },
        new Queen { PointValue = 10, IsAwake = true, PlayerId = playerId, GameId = gameId },
        new Queen { PointValue = 5, IsAwake = true, PlayerId = playerId, GameId = gameId }
    };

        game.Players = new List<Player> { player };

        // Mock GetGameStateAsync dependencies
        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);
        _mockGameRepository.Setup(r => r.GetPlayersInGameAsync(gameId)).ReturnsAsync(new List<Player> { player });
        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId)).ReturnsAsync(new List<Queen>());
        _mockGameRepository.Setup(r => r.GetDeckCardsAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetDiscardPileAsync(gameId)).ReturnsAsync(new List<GameCard>());
        _mockGameRepository.Setup(r => r.GetGameMovesAsync(gameId, 10)).ReturnsAsync(new List<Move>());

        // Act
        var winner = await _gameEngine.CheckForWinnerAsync(gameId);

        // Assert
        winner.Should().NotBeNull();
        winner!.Id.Should().Be(playerId);
        winner.Score.Should().Be(50);
    }

    [Fact]
    public async Task StartGameAsync_InsufficientPlayers_ThrowsException()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Create game with only 1 player
        var player = RepositoryMockHelper.CreateTestPlayerWithCollections(gameId: gameId);
        var players = new List<Player> { player };

        var game = RepositoryMockHelper.CreateTestGameWithCollections(
            id: gameId,
            status: GameStatus.Waiting,
            players: players);

        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(game);

        // Act & Assert
        var action = async () => await _gameEngine.StartGameAsync(gameId);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Need at least*players*");
    }

    [Fact]
    public async Task AddPlayerAsync_GameFull_ThrowsException()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        // Create game with 2 max players, already has 2 players
        var game = new Game
        {
            Id = gameId,
            Status = GameStatus.Waiting,
            MaxPlayers = 2, // Important!
            SettingsJson = JsonSerializer.Serialize(new GameSettings { MaxPlayers = 2 }) // Important!
        };

        // Add 2 players (max)
        game.Players = new List<Player>
    {
        new Player { Id = Guid.NewGuid(), Name = "Player1", GameId = gameId },
        new Player { Id = Guid.NewGuid(), Name = "Player2", GameId = gameId }
    };

        _mockGameRepository.Setup(r => r.GetByIdAsync(gameId)).ReturnsAsync(game);

        var newPlayer = new Player { Name = "NewPlayer" };

        // Act & Assert
        var action = async () => await _gameEngine.AddPlayerAsync(gameId, newPlayer);
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*full*");
    }

    [Fact]
    public async Task PlayCardAsync_KingCard_ValidMove_WakesQueen()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = RepositoryMockHelper.CreateTestGameWithCollections(
            id: gameId,
            status: GameStatus.Active);

        var player = RepositoryMockHelper.CreateTestPlayerWithCollections(
            gameId: gameId,
            isCurrentTurn: true);

        var kingCard = new Card
        {
            Id = Guid.NewGuid(),
            Type = CardType.King,
            Name = "King",
            Value = 0
        };

        player.PlayerCards = new List<PlayerCard>
    {
        new PlayerCard { Card = kingCard, CardId = kingCard.Id }
    };

        var sleepingQueen = new Queen
        {
            Id = Guid.NewGuid(),
            Type = QueenType.RoseQueen,
            Name = "Rose Queen",
            PointValue = 5,
            IsAwake = false,
            GameId = gameId
        };

        var players = new List<Player> { player };
        var sleepingQueens = new List<Queen> { sleepingQueen };

        // Setup mocks
        _mockGameRepository.SetupGetGameStateMocks(gameId, game, players);

        _mockGameRepository.Setup(r => r.GetSleepingQueensAsync(gameId))
            .ReturnsAsync(sleepingQueens);

        _mockGameRepository.Setup(r => r.GetQueenByIdAsync(sleepingQueen.Id))
            .ReturnsAsync(sleepingQueen);

        _mockGameRepository.Setup(r => r.WakeQueenAsync(sleepingQueen.Id, player.Id))
            .Returns(Task.CompletedTask);

        _mockGameRepository.Setup(r => r.RemoveCardFromPlayerHandAsync(player.Id, kingCard.Id))
            .Returns(Task.CompletedTask);

        _mockGameRepository.Setup(r => r.DiscardCardAsync(gameId, kingCard.Id))
            .Returns(Task.CompletedTask);

        _mockGameRepository.Setup(r => r.RecordMoveAsync(It.IsAny<Move>()))
            .ReturnsAsync(new Move { Id = Guid.NewGuid() });

        // Act
        var result = await _gameEngine.PlayCardAsync(
            gameId, player.Id, kingCard.Id, null, sleepingQueen.Id);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("woke");
    }
}