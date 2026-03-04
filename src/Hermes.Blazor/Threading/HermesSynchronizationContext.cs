// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.Blazor.Threading;

/// <summary>
/// SynchronizationContext for Blazor that uses direct interface calls instead of reflection.
/// Eliminates the reflection overhead from photino.Blazor's PhotinoSynchronizationContext.
/// </summary>
internal sealed class HermesSynchronizationContext : SynchronizationContext
{
    private readonly IHermesWindowBackend _backend;
    private readonly State _state;

    public HermesSynchronizationContext(IHermesWindowBackend backend)
        : this(backend, new State())
    {
    }

    private HermesSynchronizationContext(IHermesWindowBackend backend, State state)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _state = state;
    }

    /// <summary>
    /// Gets the UI thread ID directly from the backend - no reflection needed.
    /// </summary>
    public int UIThreadId => _backend.UIThreadId;

    /// <summary>
    /// Checks if we're on the UI thread - direct call, no reflection.
    /// </summary>
    public bool CheckAccess() => _backend.CheckAccess();

    public override SynchronizationContext CreateCopy()
    {
        return new HermesSynchronizationContext(_backend, _state);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        if (_backend.CheckAccess())
        {
            // Already on UI thread - execute directly
            ExecuteWithContext(d, state);
        }
        else
        {
            // Marshal to UI thread and wait for completion
            var tcs = new TaskCompletionSource();
            _backend.Invoke(() =>
            {
                try
                {
                    ExecuteWithContext(d, state);
                    tcs.SetResult();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            tcs.Task.GetAwaiter().GetResult();
        }
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        if (_backend.CheckAccess() && !_state.IsBusy)
        {
            // On UI thread and not busy - execute directly
            ExecuteWithContext(d, state);
        }
        else
        {
            // Queue for later execution - no waiting
            _backend.BeginInvoke(() => ExecuteWithContext(d, state));
        }
    }

    private void ExecuteWithContext(SendOrPostCallback d, object? state)
    {
        var previousContext = Current;
        var previousBusy = _state.IsBusy;

        try
        {
            _state.IsBusy = true;
            SetSynchronizationContext(this);
            d(state);
        }
        finally
        {
            _state.IsBusy = previousBusy;
            SetSynchronizationContext(previousContext);
        }
    }

    /// <summary>
    /// Shared state across copies of this context.
    /// </summary>
    private sealed class State
    {
        public bool IsBusy { get; set; }
    }
}
