// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Collections.Concurrent;
using Hermes.Diagnostics;

namespace Hermes.Infrastructure;

/// <summary>
/// Thread-safe work queue for marshaling callbacks onto a UI thread's message loop.
/// Coalesces wakeup requests so a burst of enqueues produces a single loop wakeup,
/// routes exceptions from fire-and-forget items to a handler instead of swallowing them,
/// and faults pending synchronous waiters on dispose so blocked callers cannot hang
/// after the window is destroyed.
/// </summary>
internal sealed class UiInvokeQueue : IDisposable
{
    private readonly ConcurrentQueue<(Action Action, TaskCompletionSource? Completion)> _items = new();
    private readonly Action<Exception> _unhandledExceptionHandler;
    private readonly object _lifetimeLock = new();
    private int _wakeupPending;
    private bool _isDisposed;

    public UiInvokeQueue(Action<Exception> unhandledExceptionHandler)
    {
        _unhandledExceptionHandler = unhandledExceptionHandler;
    }

    /// <summary>
    /// Enqueues a work item. Returns false when the queue is disposed and the item was rejected;
    /// callers decide whether rejection is an error (synchronous Invoke) or a droppable
    /// shutdown race (fire-and-forget BeginInvoke). On success, wakeupNeeded is true when the
    /// caller must wake the UI loop and false when a wakeup is already pending.
    /// Items with a completion are synchronous invokes whose exceptions fault the completion;
    /// items without one are fire-and-forget and their exceptions route to the unhandled handler.
    /// </summary>
    public bool TryEnqueue(Action action, TaskCompletionSource? completion, out bool wakeupNeeded)
    {
        lock (_lifetimeLock)
        {
            if (_isDisposed)
            {
                wakeupNeeded = false;
                return false;
            }

            _items.Enqueue((action, completion));
        }

        wakeupNeeded = Interlocked.Exchange(ref _wakeupPending, 1) == 0;
        return true;
    }

    /// <summary>
    /// Runs the items queued when it starts, on the calling thread. Call from the UI loop's wakeup handler;
    /// work queued meanwhile waits for the next wakeup, so the native loop runs between batches.
    /// </summary>
    public void Drain(Action requestWakeup)
    {
        Interlocked.Exchange(ref _wakeupPending, 0);

        var count = _items.Count;
        for (var i = 0; i < count && _items.TryDequeue(out var item); i++)
        {
            // Work behind this item gets its own wakeup, so a nested loop inside the item still runs it.
            if (!_items.IsEmpty && Interlocked.Exchange(ref _wakeupPending, 1) == 0)
                requestWakeup();

            try
            {
                item.Action();
                item.Completion?.SetResult();
            }
            catch (Exception ex)
            {
                if (item.Completion is not null)
                {
                    item.Completion.SetException(ex);
                }
                else
                {
                    ReportUnhandled(ex);
                }
            }
        }
    }

    /// <summary>
    /// Rejects new items and faults pending synchronous completions.
    /// Pending actions do not run; the owning window is being torn down.
    /// </summary>
    public void Dispose()
    {
        lock (_lifetimeLock)
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
        }

        while (_items.TryDequeue(out var item))
        {
            item.Completion?.SetException(new ObjectDisposedException(nameof(UiInvokeQueue)));
        }
    }

    private void ReportUnhandled(Exception exception)
    {
        try
        {
            _unhandledExceptionHandler(exception);
        }
        catch (Exception handlerException)
        {
            HermesLogger.Error("Dispatcher unhandled-exception handler threw.", handlerException);
        }
    }
}
