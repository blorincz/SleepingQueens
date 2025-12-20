using Microsoft.EntityFrameworkCore;
using SleepingQueens.Shared.Models.Game;
using System.Text.Json;

namespace SleepingQueens.Tests.Helpers;

public class TestApplicationDbContext : DbContext
{
    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Card> Cards { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<Queen> Queens { get; set; }
    public DbSet<GameCard> GameCards { get; set; }
    public DbSet<PlayerCard> PlayerCards { get; set; }
    public DbSet<Move> Moves { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Simplified configuration for testing
        ConfigureSimpleRelationships(modelBuilder);
        ConfigureSimpleDefaults(modelBuilder);
    }

    private static void ConfigureSimpleRelationships(ModelBuilder modelBuilder)
    {
        // Game -> Players
        modelBuilder.Entity<Game>()
            .HasMany(g => g.Players)
            .WithOne(p => p.Game)
            .HasForeignKey(p => p.GameId);

        // Game -> Queens
        modelBuilder.Entity<Game>()
            .HasMany(g => g.Queens)
            .WithOne(q => q.Game)
            .HasForeignKey(q => q.GameId);

        // Game -> GameCards
        modelBuilder.Entity<Game>()
            .HasMany(g => g.DeckCards)
            .WithOne(gc => gc.Game)
            .HasForeignKey(gc => gc.GameId);

        // Game -> Moves
        modelBuilder.Entity<Game>()
            .HasMany(g => g.Moves)
            .WithOne(m => m.Game)
            .HasForeignKey(m => m.GameId);

        // Player -> Queens
        modelBuilder.Entity<Player>()
            .HasMany(p => p.Queens)
            .WithOne(q => q.Player)
            .HasForeignKey(q => q.PlayerId);

        // Player -> PlayerCards
        modelBuilder.Entity<Player>()
            .HasMany(p => p.PlayerCards)
            .WithOne(pc => pc.Player)
            .HasForeignKey(pc => pc.PlayerId);

        // Card -> GameCards
        modelBuilder.Entity<Card>()
            .HasMany(c => c.GameCards)
            .WithOne(gc => gc.Card)
            .HasForeignKey(gc => gc.CardId);

        // Card -> PlayerCards
        modelBuilder.Entity<Card>()
            .HasMany(c => c.PlayerCards)
            .WithOne(pc => pc.Card)
            .HasForeignKey(pc => pc.CardId);
    }

    private static void ConfigureSimpleDefaults(ModelBuilder modelBuilder)
    {
        // Game defaults
        modelBuilder.Entity<Game>()
            .Property(g => g.MaxPlayers)
            .HasDefaultValue(4);

        modelBuilder.Entity<Game>()
            .Property(g => g.TargetScore)
            .HasDefaultValue(40);

        modelBuilder.Entity<Game>()
            .Property(g => g.SettingsJson)
            .HasDefaultValue(JsonSerializer.Serialize(GameSettings.Default));

        // Player defaults
        modelBuilder.Entity<Player>()
            .Property(p => p.Score)
            .HasDefaultValue(0);

        modelBuilder.Entity<Player>()
            .Property(p => p.IsCurrentTurn)
            .HasDefaultValue(false);

        // Queen defaults
        modelBuilder.Entity<Queen>()
            .Property(q => q.IsAwake)
            .HasDefaultValue(false);

        // GameCard defaults
        modelBuilder.Entity<GameCard>()
            .Property(gc => gc.Position)
            .HasDefaultValue(0);

        // Move defaults
        modelBuilder.Entity<Move>()
            .Property(m => m.TurnNumber)
            .HasDefaultValue(1);
    }
}