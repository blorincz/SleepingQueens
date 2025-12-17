using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Converters;

public class CardLocationConverter : ValueConverter<CardLocation, string>
{
    public CardLocationConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<CardLocation>(v))
    {
    }
}
