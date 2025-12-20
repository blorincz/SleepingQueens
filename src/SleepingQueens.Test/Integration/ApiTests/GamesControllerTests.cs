using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Data;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game.Enums;
using Xunit;

namespace SleepingQueens.Tests.Integration.ApiTests;

public class GamesControllerTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ApplicationDbContext _context;

    public GamesControllerTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();

        var scope = _factory.Services.CreateScope();
        _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateGame_ValidRequest_ReturnsCreatedGame()
    {
        // Arrange
        var request = new
        {
            PlayerName = "TestPlayer"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/games", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateGameResponseDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.GameId.Should().NotBe(Guid.Empty);
        result.GameCode.Should().NotBeNullOrEmpty();
        result.PlayerId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetGame_ExistingGame_ReturnsGame()
    {
        // Arrange
        var createRequest = new { PlayerName = "TestPlayer" };
        var createResponse = await _client.PostAsJsonAsync("/api/games", createRequest);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<CreateGameResponseDto>();

        // Act
        var response = await _client.GetAsync($"/api/games/{createdGame!.GameId}");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GameDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(createdGame.GameId);
        result.Code.Should().Be(createdGame.GameCode);
    }

    [Fact]
    public async Task GetGame_NonExistentGame_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/games/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartGame_ValidGame_ReturnsSuccess()
    {
        // Arrange
        // Create game with minimum players (2 for testing)
        var createRequest = new { PlayerName = "Player1" };
        var createResponse = await _client.PostAsJsonAsync("/api/games", createRequest);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<CreateGameResponseDto>();

        // Join second player
        var joinRequest = new { PlayerName = "Player2" };
        await _client.PostAsJsonAsync(
            $"/api/games/{createdGame!.GameCode}/join",
            joinRequest);

        // Act
        var response = await _client.PostAsync(
            $"/api/games/{createdGame.GameId}/start",
            null);

        // Assert
        response.EnsureSuccessStatusCode();
    }
}