using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;

namespace SleepingQueens.Data.Repositories;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid id);
    Task<IEnumerable<Card>> GetAllAsync();
    Task<IEnumerable<Card>> GetByTypeAsync(CardType type);
    Task<IEnumerable<Card>> GetNumberCardsAsync();
    Task<IEnumerable<Card>> GetSpecialCardsAsync();
    Task<Card?> GetCardByValueAsync(CardType type, int value);
    Task<IEnumerable<Card>> GetCardsByIdsAsync(IEnumerable<Guid> cardIds);
}