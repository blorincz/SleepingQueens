using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SleepingQueens.Data;
using SleepingQueens.Tests.Helpers;
using Xunit;

namespace SleepingQueens.Tests.Integration.Database;

public class DatabaseTestBase : IAsyncLifetime
{
    protected ApplicationDbContext Context { get; private set; } = null!;
    protected TestDataSeeder Seeder { get; private set; } = null!;

    private readonly IServiceProvider _serviceProvider;

    public DatabaseTestBase()
    {
        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        services.AddScoped<TestDataSeeder>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task InitializeAsync()
    {
        Context = _serviceProvider.GetRequiredService<ApplicationDbContext>();
        Seeder = _serviceProvider.GetRequiredService<TestDataSeeder>();

        await Context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.DisposeAsync();
    }
}