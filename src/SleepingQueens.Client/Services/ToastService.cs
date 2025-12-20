// SleepingQueens.Client/Services/ToastService.cs
using SleepingQueens.Client.Events;

namespace SleepingQueens.Client.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class Toast
{
    public Guid Id { get; } = Guid.NewGuid();
    public ToastLevel Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5);
}

public interface IToastService
{
    IAsyncEvent<Toast> OnToastAdded { get; }
    Task ShowToastAsync(ToastLevel level, string title, string message, TimeSpan? duration = null);
}

public class ToastService : IToastService
{
    public IAsyncEvent<Toast> OnToastAdded { get; }

    public ToastService(ILogger<ToastService> logger)
    {
        OnToastAdded = new AsyncEvent<Toast>(logger);
    }

    public async Task ShowToastAsync(ToastLevel level, string title, string message, TimeSpan? duration = null)
    {
        var toast = new Toast
        {
            Level = level,
            Title = title,
            Message = message,
            Duration = duration ?? TimeSpan.FromSeconds(5)
        };

        await OnToastAdded.InvokeAsync(toast);
    }
}