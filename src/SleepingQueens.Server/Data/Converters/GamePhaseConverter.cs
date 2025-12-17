using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Server.Data.Converters;

public class GamePhaseConverter : ValueConverter<GamePhase, string>
{
    public GamePhaseConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<GamePhase>(v))
    {
    }
}
