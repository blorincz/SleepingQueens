using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Shared.Models.Game;

namespace SleepingQueens.Data;

public static class DatabaseHelper
{
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed data if needed
        if (!await context.Cards.AnyAsync())
        {
            await SeedDataAsync(context);
        }
    }

    private static async Task SeedDataAsync(ApplicationDbContext context)
    {
        // Seed cards (you can move this to a separate seed class)
        var cards = new List<Card>();

        // Add cards here similar to SQL script
        // ... card seeding logic ...

        context.Cards.AddRange(cards);
        await context.SaveChangesAsync();
    }
}