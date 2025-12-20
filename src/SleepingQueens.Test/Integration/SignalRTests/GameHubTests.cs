using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Server.Controllers;
using SleepingQueens.Shared.Models.DTOs;
using SleepingQueens.Shared.Models.Game.Enums;
using SleepingQueens.Tests.Integration.ApiTests;
using System.Net.Http.Json;
using Xunit;

namespace SleepingQueens.Tests.Integration.SignalRTests;

public class GameHubTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HubConnection? _connection;
    private readonly HttpClient _httpClient;
    private readonly TestSignalRClientManager _clientManager;

    public GameHubTests()
    {
        _factory = new TestWebApplicationFactory();
        _httpClient = _factory.CreateClient();
        _clientManager = _factory.Services.GetRequiredService<TestSignalRClientManager>();
    }

    public async Task InitializeAsync()
    {
        // Create SignalR connection
        var handler = _factory.Server.CreateHandler();
        _connection = await _clientManager.CreateConnectionAsync(
            "http://localhost/hubs/game",
            handler);
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateGame_ApiEndpoint_ReturnsSuccess()
    {
        // Arrange
        var request = new
        {
            PlayerName = "TestPlayer"
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/games", request);

        // Assert
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateGameResponseDto>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.GameId.Should().NotBe(Guid.Empty);
        result.GameCode.Should().NotBeNullOrEmpty();
        result.PlayerId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetActiveGames_ReturnsGames()
    {
        // Arrange
        // First create a game
        var createRequest = new { PlayerName = "TestPlayer" };
        await _httpClient.PostAsJsonAsync("/api/games", createRequest);

        // Act
        var response = await _httpClient.GetAsync("/api/games/active");

        // Assert
        response.EnsureSuccessStatusCode();

        var games = await response.Content.ReadFromJsonAsync<List<GameInfoDto>>();
        games.Should().NotBeNull();
        games.Should().Contain(g => g.Status == GameStatus.Waiting);
    }

    [Fact]
    public async Task JoinGame_ValidGame_ReturnsSuccess()
    {
        // Arrange
        var createRequest = new { PlayerName = "HostPlayer" };
        var createResponse = await _httpClient.PostAsJsonAsync("/api/games", createRequest);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<CreateGameResponseDto>();

        var joinRequest = new { PlayerName = "JoiningPlayer" };

        // Act
        var joinResponse = await _httpClient.PostAsJsonAsync(
            $"/api/games/{createdGame!.GameCode}/join",
            joinRequest);

        // Assert
        joinResponse.EnsureSuccessStatusCode();

        var joinResult = await joinResponse.Content.ReadFromJsonAsync<JoinGameResponseDto>();
        joinResult.Should().NotBeNull();
        joinResult!.Success.Should().BeTrue();
        joinResult.GameId.Should().Be(createdGame.GameId);
        joinResult.PlayerId.Should().NotBe(Guid.Empty);
    }
}