using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.GameEngine;

public class Deck
{
    private readonly List<Card> _cards = [];
    private readonly Random _random = new();

    public int Count => _cards.Count;

    public Deck(IEnumerable<Card> cards)
    {
        _cards.AddRange(cards);
        Shuffle();
    }

    public void Shuffle()
    {
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public Card? Draw()
    {
        if (_cards.Count == 0) return null;

        var card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public void Return(Card card)
    {
        _cards.Add(card);
    }

    public void ReturnToBottom(Card card)
    {
        _cards.Insert(0, card);
    }

    public IEnumerable<Card> GetCards() => _cards.AsReadOnly();
}