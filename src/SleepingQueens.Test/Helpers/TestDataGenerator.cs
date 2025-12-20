using Bogus;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Tests.Helpers;

public static class TestDataGenerator
{
    private static readonly Faker _faker = new();
    private static readonly Random _random = new();

    public static Queen CreateTestQueen(
        QueenType? type = null,
        int pointValue = 5,
        Guid? playerId = null)
    {
        // If type not specified, pick a random one
        var queenType = type ?? GetRandomQueenType();

        return new Queen
        {
            Id = Guid.NewGuid(),
            Type = queenType,
            Name = $"{queenType} Queen",
            PointValue = GetPointValueForQueenType(queenType),
            ImagePath = $"/images/queens/{queenType.ToString().ToLower()}.png",
            IsAwake = playerId.HasValue,
            PlayerId = playerId
        };
    }

    private static QueenType GetRandomQueenType()
    {
        var queenTypes = Enum.GetValues<QueenType>();
        return queenTypes[_random.Next(queenTypes.Length)];
    }

    private static int GetPointValueForQueenType(QueenType type)
    {
        return type switch
        {
            QueenType.RoseQueen or
            QueenType.CatQueen or
            QueenType.DogQueen or
            QueenType.PeacockQueen => 5,

            QueenType.RainbowQueen or
            QueenType.MoonQueen or
            QueenType.SunQueen or
            QueenType.StarQueen => 10,

            QueenType.CakeQueen => 15,
            QueenType.HeartQueen => 20,

            _ => 5
        };
    }

    public static Player CreateTestPlayer(
        string? name = null,
        PlayerType type = PlayerType.Human,
        Guid? gameId = null)
    {
        return new Player
        {
            Id = Guid.NewGuid(),
            Name = name ?? _faker.Name.FirstName(),
            Type = type,
            Score = 0,
            IsCurrentTurn = false,
            GameId = gameId ?? Guid.NewGuid(),
            ConnectionId = $"test-connection-{Guid.NewGuid()}"
        };
    }

    public static Game CreateTestGame(
        string? code = null,
        GameStatus status = GameStatus.Waiting,
        int maxPlayers = 4)
    {
        return new Game
        {
            Id = Guid.NewGuid(),
            Code = code ?? _faker.Random.AlphaNumeric(6).ToUpper(),
            Status = status,
            Phase = GamePhase.Setup,
            MaxPlayers = maxPlayers,
            TargetScore = 40,
            CreatedAt = DateTime.UtcNow,
            Settings = GameSettings.Default,
            Players = new List<Player>()
        };
    }

    public static Card CreateTestCard(
        CardType type = CardType.Number,
        int value = 1,
        string? name = null)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            Type = type,
            Value = value,
            Name = name ?? type.ToString(),
            Description = $"Test {type} card",
            ImagePath = $"/images/cards/{type.ToString().ToLower()}.png"
        };
    }
}