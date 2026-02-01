using System.Text.Json;
using Hermes.Abstractions;

namespace Hermes.Testing;

/// <summary>
/// A mock window backend that records all interactions for verification in tests.
/// Also supports simulating events to test application responses.
/// </summary>
public sealed class RecordingWindowBackend : IHermesWindowBackend
{
    private readonly int _uiThreadId;
    private readonly Queue<Action> _pendingActions = new();
    private readonly object _lock = new();
    private readonly Dictionary<string, Func<string, (Stream? Content, string? ContentType)>> _customSchemes = new();

    private string _title = string.Empty;
    private (int Width, int Height) _size = (800, 600);
    private (int X, int Y) _position = (100, 100);
    private bool _isMaximized;
    private bool _isMinimized;
    private bool _isInitialized;
    private bool _isShown;
    private bool _isClosed;

    /// <summary>
    /// The recording of all interactions with this backend.
    /// </summary>
    public WindowBackendRecording Recording { get; } = new();

    /// <summary>
    /// The platform this backend simulates.
    /// </summary>
    public HermesPlatform Platform { get; set; } = HermesPlatform.Windows;

    /// <summary>
    /// Whether the window has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Whether the window is currently shown.
    /// </summary>
    public bool IsShown => _isShown;

    /// <summary>
    /// Whether the window has been closed.
    /// </summary>
    public bool IsClosed => _isClosed;

    /// <summary>
    /// The options passed during initialization.
    /// </summary>
    public HermesWindowOptions? InitialOptions { get; private set; }

    public RecordingWindowBackend()
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    #region IHermesWindowBackend Implementation

    public int UIThreadId => _uiThreadId;

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public string Title
    {
        get => _title;
        set
        {
            var oldValue = _title;
            _title = value;
            Recording.RecordPropertyChange(nameof(Title), oldValue, value);
        }
    }

    public (int Width, int Height) Size
    {
        get => _size;
        set
        {
            var oldValue = _size;
            _size = value;
            Recording.RecordPropertyChange(nameof(Size), oldValue, value);
        }
    }

    public (int X, int Y) Position
    {
        get => _position;
        set
        {
            var oldValue = _position;
            _position = value;
            Recording.RecordPropertyChange(nameof(Position), oldValue, value);
        }
    }

    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            var oldValue = _isMaximized;
            _isMaximized = value;
            Recording.RecordPropertyChange(nameof(IsMaximized), oldValue, value);

            if (value && !oldValue)
            {
                Recording.RecordEvent(nameof(Maximized));
                Maximized?.Invoke();
            }
            else if (!value && oldValue)
            {
                Recording.RecordEvent(nameof(Restored));
                Restored?.Invoke();
            }
        }
    }

    public bool IsMinimized
    {
        get => _isMinimized;
        set
        {
            var oldValue = _isMinimized;
            _isMinimized = value;
            Recording.RecordPropertyChange(nameof(IsMinimized), oldValue, value);
        }
    }

    public void Initialize(HermesWindowOptions options)
    {
        Recording.RecordMethodCall(nameof(Initialize), options);
        InitialOptions = options;
        _title = options.Title;
        _size = (options.Width, options.Height);
        if (options.X.HasValue && options.Y.HasValue)
        {
            _position = (options.X.Value, options.Y.Value);
        }
        _isMaximized = options.Maximized;
        _isMinimized = options.Minimized;

        // Record initial navigation from options
        if (!string.IsNullOrEmpty(options.StartUrl))
        {
            Recording.RecordNavigation(options.StartUrl);
        }

        _isInitialized = true;
    }

    public void Show()
    {
        Recording.RecordMethodCall(nameof(Show));
        _isShown = true;
    }

    public void Close()
    {
        Recording.RecordMethodCall(nameof(Close));
        Recording.RecordEvent(nameof(Closing));
        Closing?.Invoke();
        _isClosed = true;
    }

    public void WaitForClose()
    {
        Recording.RecordMethodCall(nameof(WaitForClose));
        Show();
        // In test mode, we don't actually block
    }

    public void NavigateToUrl(string url)
    {
        Recording.RecordMethodCall(nameof(NavigateToUrl), url);
        Recording.RecordNavigation(url);
    }

    public void NavigateToString(string html)
    {
        Recording.RecordMethodCall(nameof(NavigateToString), html);
    }

    public void SendWebMessage(string message)
    {
        Recording.RecordMethodCall(nameof(SendWebMessage), message);
        Recording.RecordWebMessageSent(message);
    }

    public void RegisterCustomScheme(string scheme, Func<string, (Stream? Content, string? ContentType)> handler)
    {
        Recording.RecordMethodCall(nameof(RegisterCustomScheme), scheme);
        _customSchemes[scheme] = handler;
    }

    public void Invoke(Action action)
    {
        Recording.RecordMethodCall(nameof(Invoke));
        if (CheckAccess())
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

    public void BeginInvoke(Action action)
    {
        Recording.RecordMethodCall(nameof(BeginInvoke));
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

    public void Dispose()
    {
        Recording.RecordMethodCall(nameof(Dispose));
    }

    #endregion

    #region Events

    public event Action? Closing;
    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action? FocusIn;
    public event Action? FocusOut;
    public event Action<string>? WebMessageReceived;
    public event Action? Maximized;
    public event Action? Restored;

    #endregion

    #region Event Simulation

    /// <summary>
    /// Simulate a web message being received from JavaScript.
    /// This will invoke any registered WebMessageReceived handlers.
    /// </summary>
    public void SimulateWebMessage(string message)
    {
        Recording.RecordWebMessageReceived(message);
        Recording.RecordEvent(nameof(WebMessageReceived), message);

        // Parse drag messages for titlebar testing
        if (message.Contains("hermes-drag", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                if (doc.RootElement.TryGetProperty("action", out var actionProp))
                {
                    Recording.RecordDragAction(actionProp.GetString() ?? "unknown");
                }
            }
            catch
            {
                // Not a valid drag message, ignore
            }
        }

        WebMessageReceived?.Invoke(message);
    }

    /// <summary>
    /// Simulate a window resize.
    /// </summary>
    public void SimulateResize(int width, int height)
    {
        var oldSize = _size;
        _size = (width, height);
        Recording.RecordPropertyChange(nameof(Size), oldSize, _size);
        Recording.RecordEvent(nameof(Resized), width, height);
        Resized?.Invoke(width, height);
    }

    /// <summary>
    /// Simulate a window move.
    /// </summary>
    public void SimulateMove(int x, int y)
    {
        var oldPosition = _position;
        _position = (x, y);
        Recording.RecordPropertyChange(nameof(Position), oldPosition, _position);
        Recording.RecordEvent(nameof(Moved), x, y);
        Moved?.Invoke(x, y);
    }

    /// <summary>
    /// Simulate the window being maximized.
    /// </summary>
    public void SimulateMaximize()
    {
        if (!_isMaximized)
        {
            _isMaximized = true;
            Recording.RecordPropertyChange(nameof(IsMaximized), false, true);
            Recording.RecordEvent(nameof(Maximized));
            Maximized?.Invoke();
        }
    }

    /// <summary>
    /// Simulate the window being restored from maximized state.
    /// </summary>
    public void SimulateRestore()
    {
        if (_isMaximized)
        {
            _isMaximized = false;
            Recording.RecordPropertyChange(nameof(IsMaximized), true, false);
            Recording.RecordEvent(nameof(Restored));
            Restored?.Invoke();
        }
    }

    /// <summary>
    /// Simulate the window gaining focus.
    /// </summary>
    public void SimulateFocusIn()
    {
        Recording.RecordEvent(nameof(FocusIn));
        FocusIn?.Invoke();
    }

    /// <summary>
    /// Simulate the window losing focus.
    /// </summary>
    public void SimulateFocusOut()
    {
        Recording.RecordEvent(nameof(FocusOut));
        FocusOut?.Invoke();
    }

    /// <summary>
    /// Simulate a click on a drag region by sending the appropriate web message.
    /// </summary>
    public void SimulateDragRegionClick()
    {
        SimulateWebMessage("""{"type":"hermes-drag","action":"drag"}""");
    }

    /// <summary>
    /// Simulate a double-click on a drag region by sending the appropriate web message.
    /// </summary>
    public void SimulateDragRegionDoubleClick()
    {
        SimulateWebMessage("""{"type":"hermes-drag","action":"double-click"}""");
    }

    /// <summary>
    /// Simulate a click on a non-drag region by sending the appropriate web message.
    /// </summary>
    public void SimulateNonDragRegionClick()
    {
        SimulateWebMessage("""{"type":"hermes-drag","action":"no-drag"}""");
    }

    #endregion

    #region Custom Scheme Testing

    /// <summary>
    /// Test a registered custom scheme handler by invoking it with a URL.
    /// </summary>
    /// <returns>The content and content type returned by the handler, or null if the scheme is not registered.</returns>
    public (Stream? Content, string? ContentType)? TestCustomScheme(string scheme, string url)
    {
        if (_customSchemes.TryGetValue(scheme, out var handler))
        {
            return handler(url);
        }
        return null;
    }

    /// <summary>
    /// Check if a custom scheme has been registered.
    /// </summary>
    public bool HasCustomScheme(string scheme) => _customSchemes.ContainsKey(scheme);

    #endregion

    #region Pending Actions

    /// <summary>
    /// Process any pending actions queued via BeginInvoke.
    /// Call this from tests to simulate the UI thread processing.
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

    #endregion
}
