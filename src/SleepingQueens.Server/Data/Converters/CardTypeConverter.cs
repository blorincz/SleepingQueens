using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Server.Data.Converters;

public class CardTypeConverter : ValueConverter<CardType, string>
{
    public CardTypeConverter() : base(
        v => v.ToString(),
        v => (CardType)Enum.Parse(typeof(CardType), v))
    {
    }
}

public class GameStatusConverter : ValueConverter<GameStatus, string>
{
    public GameStatusConverter() : base(
        v => v.ToString(),
        v => (GameStatus)Enum.Parse(typeof(GameStatus), v))
    {
    }
}

public class PlayerTypeConverter : ValueConverter<PlayerType, string>
{
    public PlayerTypeConverter() : base(
        v => v.ToString(),
        v => (PlayerType)Enum.Parse(typeof(PlayerType), v))
    {
    }
}

public class MoveTypeConverter : ValueConverter<MoveType, string>
{
    public MoveTypeConverter() : base(
        v => v.ToString(),
        v => (MoveType)Enum.Parse(typeof(MoveType), v))
    {
    }
}