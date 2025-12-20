namespace SleepingQueens.Client.Events;

public interface IAsyncEvent<TEventArgs>
{
    void Subscribe(Func<TEventArgs, Task> handler);
    void Unsubscribe(Func<TEventArgs, Task> handler);
    Task InvokeAsync(TEventArgs eventArgs);
}

public class AsyncEvent<TEventArgs> : IAsyncEvent<TEventArgs>
{
    private readonly List<Func<TEventArgs, Task>> _handlers = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger? _logger;

    public AsyncEvent(ILogger logger)
    {
        _logger = logger;
    }

    public void Subscribe(Func<TEventArgs, Task> handler)
    {
        _handlers.Add(handler);
        _logger?.LogDebug("Handler subscribed to AsyncEvent<{EventType}>. Total handlers: {Count}",
            typeof(TEventArgs).Name, _handlers.Count);
    }

    public void Unsubscribe(Func<TEventArgs, Task> handler)
    {
        _handlers.Remove(handler);
        _logger?.LogDebug("Handler unsubscribed from AsyncEvent<{EventType}>. Total handlers: {Count}",
            typeof(TEventArgs).Name, _handlers.Count);
    }

    public async Task InvokeAsync(TEventArgs eventArgs)
    {
        if (_handlers.Count == 0)
            return;

        await _semaphore.WaitAsync();
        try
        {
            var tasks = _handlers.Select(handler => SafeInvokeHandlerAsync(handler, eventArgs));
            await Task.WhenAll(tasks);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SafeInvokeHandlerAsync(Func<TEventArgs, Task> handler, TEventArgs eventArgs)
    {
        try
        {
            await handler(eventArgs);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error invoking async event handler for {EventType}", typeof(TEventArgs).Name);
        }
    }
}

// Void version (no parameters)
public class AsyncEvent : IAsyncEvent
{
    private readonly List<Func<Task>> _handlers = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<AsyncEvent>? _logger;

    public AsyncEvent(ILogger<AsyncEvent>? logger = null)
    {
        _logger = logger;
    }

    public void Subscribe(Func<Task> handler)
    {
        _handlers.Add(handler);
        _logger?.LogDebug("Handler subscribed to AsyncEvent. Total handlers: {Count}", _handlers.Count);
    }

    public void Unsubscribe(Func<Task> handler)
    {
        _handlers.Remove(handler);
        _logger?.LogDebug("Handler unsubscribed from AsyncEvent. Total handlers: {Count}", _handlers.Count);
    }

    public async Task InvokeAsync()
    {
        if (_handlers.Count == 0)
            return;

        await _semaphore.WaitAsync();
        try
        {
            var tasks = _handlers.Select(handler => SafeInvokeHandlerAsync(handler));
            await Task.WhenAll(tasks);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SafeInvokeHandlerAsync(Func<Task> handler)
    {
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error invoking async event handler");
        }
    }
}

public interface IAsyncEvent
{
    void Subscribe(Func<Task> handler);
    void Unsubscribe(Func<Task> handler);
    Task InvokeAsync();
}