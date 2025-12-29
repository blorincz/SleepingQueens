using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Converters;

public class AILevelConverter : ValueConverter<AILevel, string>
{
    public AILevelConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<AILevel>(v))
    {
    }
}
