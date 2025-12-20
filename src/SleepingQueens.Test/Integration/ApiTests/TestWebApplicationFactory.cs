using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Data;
using SleepingQueens.Tests.Helpers;

namespace SleepingQueens.Tests.Integration.ApiTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext configuration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            // Add in-memory database for testing
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
            });

            // Configure test services
            services.AddScoped<TestDataSeeder>();
            services.AddSingleton<TestSignalRClientManager>();
        });

        builder.UseEnvironment("Development");
    }
}

public class TestSignalRClientManager
{
    private readonly Dictionary<string, HubConnection> _connections = new();
    private readonly List<IDisposable> _disposables = new();

    public async Task<HubConnection> CreateConnectionAsync(string url, HttpMessageHandler? handler = null)
    {
        var connectionBuilder = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => handler ?? new HttpClientHandler();
                options.SkipNegotiation = true;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
            });

        var connection = connectionBuilder.Build();

        // Handle reconnections and errors
        connection.Closed += async (error) =>
        {
            await Task.Delay(new Random().Next(0, 5) * 1000);
            await connection.StartAsync();
        };

        await connection.StartAsync();
        _connections[connection.ConnectionId!] = connection;
        return connection;
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var connection in _connections.Values)
        {
            try
            {
                await connection.StopAsync();
                await connection.DisposeAsync();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        _connections.Clear();

        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}