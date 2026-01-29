using Hermes.Abstractions;

namespace Hermes.Benchmarks.Mocks;

/// <summary>
/// Mock IHermesWindowBackend for benchmarking sync context overhead.
/// Simulates UI thread marshaling without creating actual windows.
/// </summary>
internal sealed class MockHermesWindowBackend : IHermesWindowBackend
{
    private readonly int _uiThreadId;
    private readonly Queue<Action> _pendingActions = new();
    private readonly object _lock = new();

    public MockHermesWindowBackend()
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    public int UIThreadId => _uiThreadId;

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
        }
        else
        {
            // In real backend this would marshal to UI thread
            // For benchmarking, we simulate the overhead
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

    public void BeginInvoke(Action action)
    {
        if (CheckAccess())
        {
            action();
        }
        else
        {
            lock (_lock)
            {
                _pendingActions.Enqueue(action);
            }
        }
    }

    /// <summary>
    /// Process any pending actions. Call this from the "UI thread" in tests.
    /// </summary>
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

    // Unused members for this benchmark - minimal implementation
    public string Title { get; set; } = string.Empty;
    public (int Width, int Height) Size { get; set; }
    public (int X, int Y) Position { get; set; }
    public bool IsMaximized { get; set; }
    public bool IsMinimized { get; set; }
    public HermesPlatform Platform => HermesPlatform.macOS;

    public void Initialize(HermesWindowOptions options) { }
    public void Show() { }
    public void Close() { }
    public void WaitForClose() { }
    public void NavigateToUrl(string url) { }
    public void NavigateToString(string html) { }
    public void SendWebMessage(string message) { }
    public void RegisterCustomScheme(string scheme, Func<string, (Stream? Content, string? ContentType)> handler) { }
    public void Dispose() { }

    public event Action? Closing;
    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action? FocusIn;
    public event Action? FocusOut;
    public event Action<string>? WebMessageReceived;
    public event Action? Maximized;
    public event Action? Restored;

    // Suppress warnings for unused events
    private void SuppressWarnings()
    {
        Closing?.Invoke();
        Resized?.Invoke(0, 0);
        Moved?.Invoke(0, 0);
        FocusIn?.Invoke();
        FocusOut?.Invoke();
        WebMessageReceived?.Invoke(string.Empty);
        Maximized?.Invoke();
        Restored?.Invoke();
    }
}
