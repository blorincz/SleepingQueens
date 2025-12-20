using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Data;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Tests.Helpers;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SleepingQueens.Tests.Integration.ApiTests;

public class PlayersControllerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ApplicationDbContext _context;
    private readonly IServiceScope _scope;
    private readonly TestDataSeeder _seeder;

    public PlayersControllerTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();

        // Create a scope to resolve scoped services
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _seeder = _scope.ServiceProvider.GetRequiredService<TestDataSeeder>();
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        _scope.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetPlayer_ExistingPlayer_ReturnsPlayer()
    {
        // Arrange
        var player = TestDataGenerator.CreateTestPlayer();
        await _seeder.SeedPlayerAsync(player);

        // Act
        var response = await _client.GetAsync($"/api/players/{player.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PlayerDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(player.Id);
        result.Name.Should().Be(player.Name);
        result.Id.Should().Be(player.GameId);
    }

    [Fact]
    public async Task GetPlayer_NonExistentPlayer_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/players/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPlayersInGame_ExistingGame_ReturnsPlayers()
    {
        // Arrange
        var game = TestDataGenerator.CreateTestGame();
        var player1 = TestDataGenerator.CreateTestPlayer(gameId: game.Id);
        var player2 = TestDataGenerator.CreateTestPlayer(gameId: game.Id);

        game.Players.Add(player1);
        game.Players.Add(player2);

        await _seeder.SeedGameAsync(game);

        // Act
        var response = await _client.GetAsync($"/api/players/game/{game.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<PlayerDto>>();
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result.Should().Contain(p => p.Name == player1.Name);
        result.Should().Contain(p => p.Name == player2.Name);
    }

    [Fact]
    public async Task GetPlayersInGame_EmptyGame_ReturnsEmptyList()
    {
        // Arrange
        var game = TestDataGenerator.CreateTestGame();
        await _seeder.SeedGameAsync(game);

        // Act
        var response = await _client.GetAsync($"/api/players/game/{game.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<PlayerDto>>();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }
}