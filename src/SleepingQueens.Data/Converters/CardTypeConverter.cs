using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Converters;

public class CardTypeConverter : ValueConverter<CardType, string>
{
    public CardTypeConverter() : base(
        v => v.ToString(),
        v => Enum.Parse<CardType>(v))
    {
    }
}