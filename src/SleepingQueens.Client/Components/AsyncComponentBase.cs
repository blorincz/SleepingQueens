// Minimal AsyncComponentBase
using Microsoft.AspNetCore.Components;
using SleepingQueens.Client.Events;

namespace SleepingQueens.Client.Components;

public abstract class AsyncComponentBase : ComponentBase, IAsyncDisposable
{
    private readonly List<IDisposable> _eventSubscriptions = new();
    private readonly List<IAsyncDisposable> _asyncEventSubscriptions = new();
    private CancellationTokenSource? _cts;
    protected CancellationToken CancellationToken => (_cts ??= new CancellationTokenSource()).Token;

    // Helper methods for subscribing to async events only
    protected void SubscribeToEvent<TEventArgs>(IAsyncEvent<TEventArgs> asyncEvent, Func<TEventArgs, Task> handler)
    {
        asyncEvent.Subscribe(handler);
        _eventSubscriptions.Add(new EventSubscription<TEventArgs>(asyncEvent, handler));
    }

    protected void SubscribeToEvent(IAsyncEvent asyncEvent, Func<Task> handler)
    {
        asyncEvent.Subscribe(handler);
        _eventSubscriptions.Add(new EventSubscription(asyncEvent, handler));
    }

    // Safe async execution helper
    protected async Task ExecuteSafeAsync(Func<Task> operation, Action<Exception>? onError = null)
    {
        try
        {
            await operation();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onError?.Invoke(ex);
            Console.WriteLine($"Error in ExecuteSafeAsync: {ex.Message}");
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        // Unsubscribe from all events
        foreach (var subscription in _eventSubscriptions)
        {
            subscription.Dispose();
        }
        _eventSubscriptions.Clear();

        foreach (var subscription in _asyncEventSubscriptions)
        {
            await subscription.DisposeAsync();
        }
        _asyncEventSubscriptions.Clear();

        GC.SuppressFinalize(this);
    }

    // Helper classes for event subscriptions
    private class EventSubscription<TEventArgs> : IDisposable
    {
        private readonly IAsyncEvent<TEventArgs> _event;
        private readonly Func<TEventArgs, Task> _handler;

        public EventSubscription(IAsyncEvent<TEventArgs> @event, Func<TEventArgs, Task> handler)
        {
            _event = @event;
            _handler = handler;
        }

        public void Dispose()
        {
            _event.Unsubscribe(_handler);
        }
    }

    private class EventSubscription : IDisposable
    {
        private readonly IAsyncEvent _event;
        private readonly Func<Task> _handler;

        public EventSubscription(IAsyncEvent @event, Func<Task> handler)
        {
            _event = @event;
            _handler = handler;
        }

        public void Dispose()
        {
            _event.Unsubscribe(_handler);
        }
    }
}