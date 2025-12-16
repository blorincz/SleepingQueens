using Microsoft.EntityFrameworkCore;
using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Server.Data.Repositories;

public class CardRepository(ApplicationDbContext context) : ICardRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Card?> GetByIdAsync(Guid id)
    {
        return await _context.Cards.FindAsync(id);
    }

    public async Task<IEnumerable<Card>> GetAllAsync()
    {
        return await _context.Cards.ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetByTypeAsync(CardType type)
    {
        return await _context.Cards
            .Where(c => c.Type == type)
            .ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetNumberCardsAsync()
    {
        return await _context.Cards
            .Where(c => c.Type == CardType.Number)
            .ToListAsync();
    }

    public async Task<IEnumerable<Card>> GetSpecialCardsAsync()
    {
        return await _context.Cards
            .Where(c => c.Type != CardType.Number && c.Type != CardType.Queen)
            .ToListAsync();
    }

    public async Task<Card?> GetCardByValueAsync(CardType type, int value)
    {
        return await _context.Cards
            .FirstOrDefaultAsync(c => c.Type == type && c.Value == value);
    }

    public async Task<IEnumerable<Card>> GetCardsByIdsAsync(IEnumerable<Guid> cardIds)
    {
        return await _context.Cards
            .Where(c => cardIds.Contains(c.Id))
            .ToListAsync();
    }
}