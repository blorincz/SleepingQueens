using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Data.Converters;

public class QueenTypeConverter : ValueConverter<QueenType, string>
{
    public QueenTypeConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<QueenType>(v))
    {
    }
}