using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Client.Services;

public interface ICardImageService
{
    string GetCardImagePath(CardDto card);
    string GetCardBackImagePath();
    string GetQueenImagePath(QueenDto queen);
    string GetQueenBackImagePath();
    string GetFallbackCardImage(CardType cardType);
    string GetFallbackQueenImage(int pointValue);
}

public class CardImageService : ICardImageService
{
    private const string CardBackPath = "/images/cards/back.png";
    private const string QueenBackPath = "/images/cards/backqueen.png";

    public string GetCardImagePath(CardDto card)
    {
        if (!string.IsNullOrEmpty(card.ImagePath))
            return card.ImagePath;

        return GetFallbackCardImage(card.Type);
    }

    public string GetCardBackImagePath() => CardBackPath;

    public string GetQueenImagePath(QueenDto queen)
    {
        if (!string.IsNullOrEmpty(queen.ImagePath))
            return queen.ImagePath;

        return GetFallbackQueenImage(queen.PointValue);
    }

    public string GetQueenBackImagePath() => QueenBackPath;

    public string GetFallbackCardImage(CardType cardType)
    {
        return cardType switch
        {
            CardType.King => "/images/cards/king.png",
            CardType.Knight => "/images/cards/knight.png",
            CardType.Dragon => "/images/cards/dragon.png",
            CardType.SleepingPotion => "/images/cards/potion.png",
            CardType.Jester => "/images/cards/jester.png",
            CardType.Number => "/images/cards/number.png",
            _ => CardBackPath
        };
    }

    public string GetFallbackQueenImage(int pointValue)
    {
        return pointValue switch
        {
            20 => "/images/queens/heart.png",
            15 => "/images/queens/pancake.png",
            10 => "/images/queens/starfish.png",
            _ => "/images/queens/rose.png"
        };
    }
}