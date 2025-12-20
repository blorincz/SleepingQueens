using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Tests.Helpers;
using SleepingQueens.Tests.Integration.ApiTests;
using Xunit;

namespace SleepingQueens.Tests.Integration.SignalRTests;

public abstract class SignalRTestBase : IAsyncLifetime
{
    protected HubConnection HubConnection { get; private set; } = null!;
    protected TestWebApplicationFactory Factory { get; }
    protected TestDataSeeder Seeder { get; }

    private readonly TestSignalRClientManager _clientManager;

    protected SignalRTestBase(TestWebApplicationFactory factory)
    {
        Factory = factory;
        Seeder = factory.Services.GetRequiredService<TestDataSeeder>();
        _clientManager = factory.Services.GetRequiredService<TestSignalRClientManager>();
    }

    public async Task InitializeAsync()
    {
        HubConnection = await _clientManager.CreateConnectionAsync("http://localhost/hubs/game");
    }

    public async Task DisposeAsync()
    {
        await _clientManager.DisconnectAllAsync();
    }

    protected async Task<T> WaitForHubMessageAsync<T>(string methodName, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<T>();
        var cancellationTokenSource = new CancellationTokenSource(timeout);

        HubConnection.On<T>(methodName, data =>
        {
            tcs.TrySetResult(data);
        });

        cancellationTokenSource.Token.Register(() =>
            tcs.TrySetCanceled(cancellationTokenSource.Token));

        return await tcs.Task;
    }
}