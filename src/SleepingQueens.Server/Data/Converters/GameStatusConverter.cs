using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Server.Data.Converters;

public class GameStatusConverter : ValueConverter<GameStatus, string>
{
    public GameStatusConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<GameStatus>(v))
    {
    }
}
