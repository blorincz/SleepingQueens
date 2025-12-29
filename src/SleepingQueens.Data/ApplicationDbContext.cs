using Microsoft.EntityFrameworkCore;
using SleepingQueens.Data.Converters;
using SleepingQueens.Shared.Models.Game;
using SleepingQueens.Shared.Models.Game.Enums;
using System.Text.Json;

namespace SleepingQueens.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
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

        // Apply converters
        ApplyConverters(modelBuilder);

        // Configure relationships
        ConfigureRelationships(modelBuilder);

        // Configure constraints
        ConfigureConstraints(modelBuilder);
    }

    private static void ApplyConverters(ModelBuilder modelBuilder)
    {
        // Card converters
        modelBuilder.Entity<Card>()
            .Property(c => c.Type)
            .HasConversion(new CardTypeConverter())
            .HasMaxLength(20);

        // Game converters
        modelBuilder.Entity<Game>()
            .Property(g => g.Status)
            .HasConversion(new GameStatusConverter())
            .HasMaxLength(20);

        modelBuilder.Entity<Game>()
            .Property(g => g.Phase)
            .HasConversion(new GamePhaseConverter())
            .HasMaxLength(20);

        modelBuilder.Entity<Game>()
           .Property(g => g.SettingsJson)
           .HasColumnName("Settings")
           .HasDefaultValue(JsonSerializer.Serialize(GameSettings.Default));

        // Player converters
        modelBuilder.Entity<Player>()
            .Property(p => p.Type)
            .HasConversion(new PlayerTypeConverter())
            .HasMaxLength(20);

        modelBuilder.Entity<Player>()
           .Property(p => p.AILevel)
           .HasConversion(new AILevelConverter())
           .HasMaxLength(20);

        // Queen converter
        modelBuilder.Entity<Queen>()
            .Property(q => q.Type)
            .HasConversion(new QueenTypeConverter())
            .HasMaxLength(20);

        // GameCard converters
        modelBuilder.Entity<GameCard>()
            .Property(gc => gc.Location)
            .HasConversion(new CardLocationConverter())
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(CardLocation.Deck);

        // Move converters
        modelBuilder.Entity<Move>()
            .Property(m => m.Type)
            .HasConversion(new MoveTypeConverter())
            .HasMaxLength(20);
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        // Game -> Players (one-to-many)
        modelBuilder.Entity<Game>()
            .HasMany(g => g.Players)
            .WithOne(p => p.Game)
            .HasForeignKey(p => p.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Game -> Queens (one-to-many)
        modelBuilder.Entity<Game>()
            .HasMany(g => g.Queens)
            .WithOne(q => q.Game)
            .HasForeignKey(q => q.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Game -> GameCards (one-to-many)
        modelBuilder.Entity<Game>()
            .HasMany(g => g.DeckCards)
            .WithOne(gc => gc.Game)
            .HasForeignKey(gc => gc.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Game -> Moves (one-to-many)
        modelBuilder.Entity<Game>()
            .HasMany(g => g.Moves)
            .WithOne(m => m.Game)
            .HasForeignKey(m => m.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player -> Queens (one-to-many)
        modelBuilder.Entity<Player>()
            .HasMany(p => p.Queens)
            .WithOne(q => q.Player)
            .HasForeignKey(q => q.PlayerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Player -> PlayerCards (one-to-many)
        modelBuilder.Entity<Player>()
            .HasMany(p => p.PlayerCards)
            .WithOne(pc => pc.Player)
            .HasForeignKey(pc => pc.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Player -> Moves (one-to-many)
        modelBuilder.Entity<Player>()
            .HasMany(p => p.Moves)
            .WithOne(m => m.Player)
            .HasForeignKey(m => m.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Card -> GameCards (one-to-many)
        modelBuilder.Entity<Card>()
            .HasMany(c => c.GameCards)
            .WithOne(gc => gc.Card)
            .HasForeignKey(gc => gc.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Card -> PlayerCards (one-to-many)
        modelBuilder.Entity<Card>()
            .HasMany(c => c.PlayerCards)
            .WithOne(pc => pc.Card)
            .HasForeignKey(pc => pc.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureConstraints(ModelBuilder modelBuilder)
    {
        // Card value constraints
        modelBuilder.Entity<Card>().ToTable(t => t.HasCheckConstraint("CK_Card_Value",
                @"([Type] = 'Number' AND [Value] BETWEEN 1 AND 10) OR 
                  ([Type] IN ('King', 'Knight', 'Dragon', 'SleepingPotion', 'Jester') AND [Value] = 0) OR
                  ([Type] = 'Queen' AND [Value] BETWEEN 5 AND 20)"));        

        // Game constraints
        modelBuilder.Entity<Game>()
            .HasIndex(g => g.Code)
            .IsUnique();

        modelBuilder.Entity<Game>()
            .Property(g => g.MaxPlayers)
            .HasDefaultValue(4);

        modelBuilder.Entity<Game>()
            .Property(g => g.TargetScore)
            .HasDefaultValue(40);

        modelBuilder.Entity<Game>(entity =>
        {
            // SettingsJson column configuration
            entity.Property(g => g.SettingsJson)
                .HasColumnName("Settings")
                .HasDefaultValue(JsonSerializer.Serialize(GameSettings.Default));

            // Add computed property or index if needed
            entity.HasIndex(g => new { g.Status, g.CreatedAt });
            entity.HasIndex(g => g.Code).IsUnique();
        });

        modelBuilder.Entity<Game>()
        .ToTable(tb => tb.UseSqlOutputClause(false));

        // Player constraints
        modelBuilder.Entity<Player>()
            .Property(p => p.Score)
            .HasDefaultValue(0);

        modelBuilder.Entity<Player>()
            .Property(p => p.IsCurrentTurn)
            .HasDefaultValue(false);

        modelBuilder.Entity<Player>()
            .Property(p => p.JoinedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        modelBuilder.Entity<Player>()
        .ToTable(tb => tb.UseSqlOutputClause(false));

        // Queen constraints
        modelBuilder.Entity<Queen>()
            .Property(q => q.IsAwake)
            .HasDefaultValue(false);

        modelBuilder.Entity<Queen>()
        .ToTable(tb => tb.UseSqlOutputClause(false));

        // GameCard constraints
        modelBuilder.Entity<GameCard>()
            .Property(gc => gc.Position)
            .HasDefaultValue(0);

        // Move constraints
        modelBuilder.Entity<Move>()
            .Property(m => m.TurnNumber)
            .HasDefaultValue(1);

        modelBuilder.Entity<Move>()
            .Property(m => m.Timestamp)
            .HasDefaultValueSql("GETUTCDATE()");
    }
}