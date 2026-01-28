using System.Reflection;

namespace Hermes.Benchmarks.Mocks;

/// <summary>
/// Mock PhotinoWindow for benchmarking Photino's reflection-based sync context.
/// Replicates the fields and methods that PhotinoSynchronizationContext accesses via reflection.
/// </summary>
public sealed class MockPhotinoWindow
{
    // This field is accessed via reflection in PhotinoSynchronizationContext
    // Using the exact name that Photino looks for
    private readonly int _managedThreadId;

    private readonly Queue<Action> _pendingActions = new();
    private readonly object _lock = new();

    public MockPhotinoWindow()
    {
        _managedThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Invoke method that PhotinoSynchronizationContext calls via reflection.
    /// </summary>
    public void Invoke(Action action)
    {
        if (Environment.CurrentManagedThreadId == _managedThreadId)
        {
            action();
        }
        else
        {
            var tcs = new TaskCompletionSource();
            lock (_lock)
            {
                _pendingActions.Enqueue(() =>
                {
                    action();
                    tcs.SetResult();
                });
            }
            ProcessPending();
            tcs.Task.GetAwaiter().GetResult();
        }
    }

    public void ProcessPending()
    {
        while (true)
        {
            Action? action;
            lock (_lock)
            {
                if (_pendingActions.Count == 0)
                    break;
                action = _pendingActions.Dequeue();
            }
            action();
        }
    }
}

/// <summary>
/// Simulates Photino's reflection-based sync context approach for benchmarking.
/// This mirrors how PhotinoSynchronizationContext gets UIThreadId and Invoke method.
/// </summary>
public sealed class ReflectionBasedSyncContext : SynchronizationContext
{
    private readonly MockPhotinoWindow _window;
    private readonly int _uiThreadId;
    private readonly MethodInfo _invokeMethodInfo;

    public ReflectionBasedSyncContext(MockPhotinoWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        // This is exactly how Photino does it - reflection to get private field
        _uiThreadId = (int)typeof(MockPhotinoWindow)
            .GetField("_managedThreadId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_window)!;

        // This is how Photino gets the Invoke method
        _invokeMethodInfo = typeof(MockPhotinoWindow)
            .GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)!;
    }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        // Simulate queueing - in Photino this goes through Task continuation
        ExecuteViaReflection(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ExecuteViaReflection(() => d(state));
    }

    private void ExecuteViaReflection(Action action)
    {
        // This is the key difference - Photino uses reflection to invoke
        _invokeMethodInfo.Invoke(_window, [action]);
    }
}

/// <summary>
/// Direct interface call version (like Hermes) for comparison.
/// </summary>
public sealed class DirectCallSyncContext : SynchronizationContext
{
    private readonly MockPhotinoWindow _window;
    private readonly int _uiThreadId;

    public DirectCallSyncContext(MockPhotinoWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        // Store thread ID directly (in Hermes this comes from IHermesWindowBackend.UIThreadId)
        // We use reflection once at construction for fair comparison, but NOT on every call
        _uiThreadId = (int)typeof(MockPhotinoWindow)
            .GetField("_managedThreadId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_window)!;
    }

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public override void Post(SendOrPostCallback d, object? state)
    {
        // Direct call - no reflection on invoke
        _window.Invoke(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        // Direct call - no reflection on invoke
        _window.Invoke(() => d(state));
    }
}
