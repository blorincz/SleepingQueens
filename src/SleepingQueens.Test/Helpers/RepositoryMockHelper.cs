using Moq;
using SleepingQueens.Data.Repositories;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using System.Text.Json;

namespace SleepingQueens.Tests.Helpers;

public static class RepositoryMockHelper
{
    public static void SetupGetGameStateMocks(
    this Mock<IGameRepository> mockRepo,
    Guid gameId,
    Game game,
    List<Player> players,
    bool includeMinimumPlayers = true)
    {
        // Setup GetByIdAsync
        mockRepo.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(game);

        // Setup GetPlayersInGameAsync
        mockRepo.Setup(r => r.GetPlayersInGameAsync(gameId))
            .ReturnsAsync(players);

        // Setup other required mocks for GetGameStateAsync
        mockRepo.Setup(r => r.GetSleepingQueensAsync(gameId))
            .ReturnsAsync(new List<Queen>());

        mockRepo.Setup(r => r.GetDeckCardsAsync(gameId))
            .ReturnsAsync(new List<GameCard>());

        mockRepo.Setup(r => r.GetDiscardPileAsync(gameId))
            .ReturnsAsync(new List<GameCard>());

        mockRepo.Setup(r => r.GetGameMovesAsync(gameId, It.IsAny<int>()))
            .ReturnsAsync(new List<Move>());

        // Setup common Task-returning methods
        mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Game>()))
            .Returns(Task.CompletedTask);
    }

    public static Player CreateTestPlayerWithCollections(
        Guid? id = null,
        string? name = null,
        Guid? gameId = null,
        bool isCurrentTurn = false,
        int score = 0)
    {
        return new Player
        {
            Id = id ?? Guid.NewGuid(),
            Name = name ?? "TestPlayer",
            Type = PlayerType.Human,
            GameId = gameId ?? Guid.NewGuid(),
            IsCurrentTurn = isCurrentTurn,
            Score = score,
            ConnectionId = $"conn-{Guid.NewGuid()}",
            JoinedAt = DateTime.UtcNow,
            Queens = new List<Queen>(), // Initialize collections
            PlayerCards = new List<PlayerCard>() // Initialize collections
        };
    }

    public static Game CreateTestGameWithCollections(
    Guid? id = null,
    string? code = null,
    GameStatus status = GameStatus.Active,
    int targetScore = 40,
    List<Player>? players = null) // Add optional players parameter
    {
        var game = new Game
        {
            Id = id ?? Guid.NewGuid(),
            Code = code ?? "TEST01",
            Status = status,
            Phase = status == GameStatus.Active ? GamePhase.Playing : GamePhase.Setup,
            CurrentPlayerIndex = 0,
            MaxPlayers = 4,
            TargetScore = targetScore,
            CreatedAt = DateTime.UtcNow,
            StartedAt = status == GameStatus.Active ? DateTime.UtcNow : null,
            EndedAt = null,
            SettingsJson = JsonSerializer.Serialize(GameSettings.Default)
        };

        // Initialize players collection
        game.Players = players ?? new List<Player>();

        return game;
    }

    public static Queen CreateTestQueen(
        Guid? id = null,
        QueenType type = QueenType.RoseQueen,
        Guid? playerId = null,
        Guid? gameId = null,
        bool isAwake = false)
    {
        return new Queen
        {
            Id = id ?? Guid.NewGuid(),
            Type = type,
            Name = $"{type} Queen",
            PointValue = GetPointValueForQueenType(type),
            ImagePath = $"/images/queens/{type.ToString().ToLower()}.png",
            IsAwake = isAwake,
            PlayerId = playerId,
            GameId = gameId ?? Guid.NewGuid()
        };
    }

    private static int GetPointValueForQueenType(QueenType type)
    {
        return type switch
        {
            QueenType.RoseQueen or QueenType.StarfishQueen or
            QueenType.CakeQueen or QueenType.RainbowQueen => 5,

            QueenType.PeacockQueen or QueenType.MoonQueen or
            QueenType.SunflowerQueen or QueenType.LadybugQueen => 10,

            QueenType.CatQueen or QueenType.DogQueen or QueenType.PancakeQueen => 15,

            QueenType.HeartQueen => 20,
            _ => 5
        };
    }

    public static void SetupGetGameStateDtoMocks(
        this Mock<IGameRepository> mockRepo,
        Guid gameId,
        Game game,
        List<Player> players,
        List<Queen>? queens = null,
        List<GameCard>? deckCards = null,
        List<Move>? moves = null)
    {
        // Setup GetByIdAsync
        mockRepo.Setup(r => r.GetByIdAsync(gameId))
            .ReturnsAsync(game);

        // Setup GetPlayersInGameAsync
        mockRepo.Setup(r => r.GetPlayersInGameAsync(gameId))
            .ReturnsAsync(players);

        // Setup GetQueensForGameAsync
        mockRepo.Setup(r => r.GetQueensForGameAsync(gameId))
            .ReturnsAsync(queens ?? new List<Queen>());

        // Setup GetDeckCardsAsync
        mockRepo.Setup(r => r.GetDeckCardsAsync(gameId))
            .ReturnsAsync(deckCards ?? new List<GameCard>());

        // Setup GetGameMovesAsync
        mockRepo.Setup(r => r.GetGameMovesAsync(gameId, It.IsAny<int>()))
            .ReturnsAsync(moves ?? new List<Move>());
    }

    // Helper method to create test GameStateDto
    public static GameStateDto CreateTestGameStateDto(Game game, Player player)
    {
        var gameDto = new GameDto
        {
            Id = game.Id,
            Code = game.Code,
            Status = game.Status,
            Phase = game.Phase,
            MaxPlayers = game.MaxPlayers,
            TargetScore = game.TargetScore,
            CreatedAt = game.CreatedAt,
            StartedAt = game.StartedAt,
            EndedAt = game.EndedAt
        };

        var playerDto = new PlayerDto
        {
            Id = player.Id,
            Name = player.Name,
            Type = player.Type,
            Score = player.Score,
            IsCurrentTurn = player.IsCurrentTurn,
            Hand = player.PlayerCards.Select(pc => new CardDto
            {
                Id = pc.CardId,
                Type = pc.Card?.Type ?? CardType.Number,
                Name = pc.Card?.Name ?? "Unknown",
                Value = pc.Card?.Value ?? 0
            }).ToList()
        };

        return new GameStateDto
        {
            Game = gameDto,
            Players = new List<PlayerDto> { playerDto },
            SleepingQueens = new List<QueenDto>(),
            AwakenedQueens = new List<QueenDto>(),
            DeckCards = new List<CardDto>(),
            DiscardPile = new List<CardDto>(),
            RecentMoves = new List<MoveDto>(),
            CurrentPlayer = player.IsCurrentTurn ? playerDto : null,
            CurrentPhase = game.Phase
        };
    }
}