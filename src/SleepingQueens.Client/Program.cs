// SleepingQueens.Client/Program.cs
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SleepingQueens.Client;
using SleepingQueens.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Register services
builder.Services.AddScoped<ISignalRService, SignalRService>();
builder.Services.AddScoped<IGameStateService, GameStateService>();


// Add logging
builder.Services.AddLogging();

await builder.Build().RunAsync();