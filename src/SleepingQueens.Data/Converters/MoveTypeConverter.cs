using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Converters;

public class MoveTypeConverter : ValueConverter<MoveType, string>
{
    public MoveTypeConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<MoveType>(v))
    {
    }
}