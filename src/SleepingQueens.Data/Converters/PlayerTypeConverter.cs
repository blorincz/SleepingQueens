using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Converters;

public class PlayerTypeConverter : ValueConverter<PlayerType, string>
{
    public PlayerTypeConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<PlayerType>(v))
    {
    }
}