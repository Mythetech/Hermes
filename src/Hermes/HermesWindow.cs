using Hermes.Abstractions;
using Hermes.Diagnostics;
using Hermes.Menu;
using Hermes.Storage;

namespace Hermes;

/// <summary>
/// A cross-platform native window with an embedded WebView.
/// Use fluent methods to configure, then call WaitForClose() to show and run.
/// </summary>
public sealed class HermesWindow : IDisposable
{
    private readonly IHermesWindowBackend _backend;
    private readonly HermesWindowOptions _options = new();
    private NativeMenuBar? _nativeMenuBar;
    private IDialogBackend? _dialogBackend;
    private string? _windowStateKey;
    private bool _initialized;
    private bool _disposed;

    private const int MinPersistableSize = 10;

    // Track last known normal (non-maximized) window state for persistence
    private int _lastNormalX;
    private int _lastNormalY;
    private int _lastNormalWidth;
    private int _lastNormalHeight;

    /// <summary>
    /// Pre-warm platform resources for faster first-window creation.
    /// Call this early in application startup (e.g., at the start of Main()).
    /// On Windows, this begins WebView2 environment creation on a background thread.
    /// </summary>
    public static void Prewarm()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
        {
            Platforms.Windows.WebView2EnvironmentPool.Instance.BeginPrewarm();
        }
#endif
        // Linux and macOS: no pre-warming needed (GTK init is fast, macOS uses native WebKit)
    }

    /// <summary>
    /// Create a new Hermes window for the current platform.
    /// </summary>
    public HermesWindow()
    {
        _backend = CreatePlatformBackend();
    }

    /// <summary>
    /// Create a Hermes window with a custom backend.
    /// Used by Hermes.Testing for mock/recording backends.
    /// </summary>
    internal HermesWindow(IHermesWindowBackend backend)
    {
        _backend = backend;
    }

    #region Fluent Configuration (before initialization)

    /// <summary>
    /// Set the window title.
    /// </summary>
    public HermesWindow SetTitle(string title)
    {
        ThrowIfInitialized();
        _options.Title = title;
        return this;
    }

    /// <summary>
    /// Set the initial window size.
    /// </summary>
    public HermesWindow SetSize(int width, int height)
    {
        ThrowIfInitialized();
        _options.Width = width;
        _options.Height = height;
        return this;
    }

    /// <summary>
    /// Set the initial window position.
    /// </summary>
    public HermesWindow SetPosition(int x, int y)
    {
        ThrowIfInitialized();
        _options.X = x;
        _options.Y = y;
        _options.CenterOnScreen = false;
        return this;
    }

    /// <summary>
    /// Center the window on screen when shown.
    /// </summary>
    public HermesWindow Center()
    {
        ThrowIfInitialized();
        _options.CenterOnScreen = true;
        return this;
    }

    /// <summary>
    /// Set whether the window is resizable.
    /// </summary>
    public HermesWindow SetResizable(bool resizable)
    {
        ThrowIfInitialized();
        _options.Resizable = resizable;
        return this;
    }

    /// <summary>
    /// Set whether the window is chromeless (no title bar or borders).
    /// </summary>
    public HermesWindow SetChromeless(bool chromeless)
    {
        ThrowIfInitialized();
        _options.Chromeless = chromeless;
        return this;
    }

    /// <summary>
    /// Set the window icon.
    /// </summary>
    public HermesWindow SetIcon(string iconPath)
    {
        ThrowIfInitialized();
        _options.IconPath = iconPath;
        return this;
    }

    /// <summary>
    /// Start the window maximized.
    /// </summary>
    public HermesWindow Maximize()
    {
        ThrowIfInitialized();
        _options.Maximized = true;
        _options.Minimized = false;
        return this;
    }

    /// <summary>
    /// Start the window minimized.
    /// </summary>
    public HermesWindow Minimize()
    {
        ThrowIfInitialized();
        _options.Minimized = true;
        _options.Maximized = false;
        return this;
    }

    /// <summary>
    /// Keep the window always on top.
    /// </summary>
    public HermesWindow SetTopMost(bool topMost)
    {
        ThrowIfInitialized();
        _options.TopMost = topMost;
        return this;
    }

    /// <summary>
    /// Set minimum window size constraints.
    /// </summary>
    public HermesWindow SetMinSize(int width, int height)
    {
        ThrowIfInitialized();
        _options.MinWidth = width;
        _options.MinHeight = height;
        return this;
    }

    /// <summary>
    /// Set maximum window size constraints.
    /// </summary>
    public HermesWindow SetMaxSize(int width, int height)
    {
        ThrowIfInitialized();
        _options.MaxWidth = width;
        _options.MaxHeight = height;
        return this;
    }

    /// <summary>
    /// Enable or disable developer tools.
    /// </summary>
    public HermesWindow SetDevToolsEnabled(bool enabled)
    {
        ThrowIfInitialized();
        _options.DevToolsEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Enable or disable the WebView context menu.
    /// </summary>
    public HermesWindow SetContextMenuEnabled(bool enabled)
    {
        ThrowIfInitialized();
        _options.ContextMenuEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Enable custom title bar mode.
    /// On macOS: Uses transparent title bar with native traffic light buttons.
    /// On Windows/Linux: Enables chromeless mode for fully custom window chrome.
    /// </summary>
    public HermesWindow SetCustomTitleBar(bool enabled)
    {
        ThrowIfInitialized();
        _options.CustomTitleBar = enabled;
        return this;
    }

    /// <summary>
    /// Enable window state persistence. Window position, size, and maximized state
    /// will be saved on close and restored on next launch.
    /// </summary>
    /// <param name="key">Optional key identifying this window. Defaults to the window title if not specified.</param>
    public HermesWindow RememberWindowState(string? key = null)
    {
        ThrowIfInitialized();
        // Use empty string as sentinel to indicate "use title as key"
        _options.WindowStateKey = key ?? string.Empty;
        return this;
    }

    #endregion

    #region Content

    /// <summary>
    /// Load a URL in the WebView.
    /// </summary>
    public HermesWindow Load(string url)
    {
        if (_initialized)
        {
            _backend.NavigateToUrl(url);
        }
        else
        {
            _options.StartUrl = url;
            _options.StartHtml = null;
        }
        return this;
    }

    /// <summary>
    /// Load HTML content directly in the WebView.
    /// </summary>
    public HermesWindow LoadHtml(string html)
    {
        if (_initialized)
        {
            _backend.NavigateToString(html);
        }
        else
        {
            _options.StartHtml = html;
            _options.StartUrl = null;
        }
        return this;
    }

    /// <summary>
    /// Register a custom URL scheme handler.
    /// </summary>
    public HermesWindow RegisterCustomScheme(string scheme, Func<string, (Stream? Content, string? ContentType)> handler)
    {
        _backend.RegisterCustomScheme(scheme, handler);
        return this;
    }

    #endregion

    #region Events

    /// <summary>
    /// Register a handler for when the window is closing.
    /// </summary>
    public HermesWindow OnClosing(Action handler)
    {
        _backend.Closing += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is resized.
    /// </summary>
    public HermesWindow OnResized(Action<int, int> handler)
    {
        _backend.Resized += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is moved.
    /// </summary>
    public HermesWindow OnMoved(Action<int, int> handler)
    {
        _backend.Moved += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for when the window gains focus.
    /// </summary>
    public HermesWindow OnFocusIn(Action handler)
    {
        _backend.FocusIn += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for when the window loses focus.
    /// </summary>
    public HermesWindow OnFocusOut(Action handler)
    {
        _backend.FocusOut += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for messages from JavaScript.
    /// </summary>
    public HermesWindow OnWebMessage(Action<string> handler)
    {
        _backend.WebMessageReceived += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is maximized.
    /// </summary>
    public HermesWindow OnMaximized(Action handler)
    {
        _backend.Maximized += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is restored from maximized state.
    /// </summary>
    public HermesWindow OnRestored(Action handler)
    {
        _backend.Restored += handler;
        return this;
    }

    #endregion

    #region Menu and Dialogs

    /// <summary>
    /// Access the native menu bar for this window.
    /// Provides a fluent API for building menus with runtime modification support.
    /// </summary>
    public NativeMenuBar MenuBar
    {
        get
        {
            if (_nativeMenuBar is null)
            {
                var backend = CreatePlatformMenuBackend();
                _nativeMenuBar = new NativeMenuBar(backend);
            }
            return _nativeMenuBar;
        }
    }

    /// <summary>
    /// Access native dialogs (file open/save, message boxes).
    /// </summary>
    public IDialogBackend Dialogs
    {
        get
        {
            _dialogBackend ??= CreatePlatformDialogBackend();
            return _dialogBackend;
        }
    }

    /// <summary>
    /// Create a new context menu for this window.
    /// Context menus can be shown at any screen position using Show(x, y).
    /// </summary>
    /// <returns>A new NativeContextMenu instance.</returns>
    public NativeContextMenu CreateContextMenu()
    {
        var backend = CreatePlatformContextMenuBackend();
        return new NativeContextMenu(backend);
    }

    #endregion

    #region Lifecycle

    private const string DefaultLoadingHtml = """
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {
                    margin: 0;
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    height: 100vh;
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    background: #f5f5f5;
                    color: #333;
                }
                @media (prefers-color-scheme: dark) {
                    body { background: #1a1a1a; color: #e0e0e0; }
                }
                .loader {
                    width: 24px;
                    height: 24px;
                    border: 3px solid #ddd;
                    border-top-color: #3498db;
                    border-radius: 50%;
                    animation: spin 1s linear infinite;
                }
                @keyframes spin { to { transform: rotate(360deg); } }
            </style>
        </head>
        <body><div class="loader"></div></body>
        </html>
        """;

    /// <summary>
    /// Show the window immediately with loading content, then return.
    /// Use this for faster perceived startup when Blazor initialization can happen after the window is visible.
    /// </summary>
    /// <param name="loadingHtml">Optional custom HTML to display while loading. Defaults to a simple spinner.</param>
    public void ShowWithLoadingState(string? loadingHtml = null)
    {
        // Set the initial content to the loading HTML
        _options.StartHtml = loadingHtml ?? DefaultLoadingHtml;
        _options.StartUrl = null;

        EnsureInitialized();
        _backend.Show();
    }

    /// <summary>
    /// Show the window and return immediately.
    /// </summary>
    public void Show()
    {
        EnsureInitialized();
        _backend.Show();
    }

    /// <summary>
    /// Show the window and block until it is closed.
    /// </summary>
    public void WaitForClose()
    {
        EnsureInitialized();
        _backend.WaitForClose();
    }

    /// <summary>
    /// Close the window.
    /// </summary>
    public void Close()
    {
        if (_initialized)
        {
            _backend.Close();
        }
    }

    #endregion

    #region Post-Initialization Operations

    /// <summary>
    /// Get or set the window title.
    /// </summary>
    public string Title
    {
        get => _initialized ? _backend.Title : _options.Title;
        set
        {
            if (_initialized)
                _backend.Title = value;
            else
                _options.Title = value;
        }
    }

    /// <summary>
    /// Get or set the window size.
    /// </summary>
    public (int Width, int Height) Size
    {
        get => _initialized ? _backend.Size : (_options.Width, _options.Height);
        set
        {
            if (_initialized)
                _backend.Size = value;
            else
            {
                _options.Width = value.Width;
                _options.Height = value.Height;
            }
        }
    }

    /// <summary>
    /// Get or set the window position.
    /// </summary>
    public (int X, int Y) Position
    {
        get => _initialized ? _backend.Position : (_options.X ?? 0, _options.Y ?? 0);
        set
        {
            if (_initialized)
                _backend.Position = value;
            else
            {
                _options.X = value.X;
                _options.Y = value.Y;
            }
        }
    }

    /// <summary>
    /// Send a message to JavaScript in the WebView.
    /// </summary>
    public void SendMessage(string message)
    {
        EnsureInitialized();
        _backend.SendWebMessage(message);
    }

    /// <summary>
    /// Execute an action on the UI thread.
    /// </summary>
    public void Invoke(Action action)
    {
        if (_initialized)
            _backend.Invoke(action);
        else
            action();
    }

    /// <summary>
    /// Gets whether the window is currently maximized.
    /// </summary>
    public bool IsMaximized => _initialized && _backend.IsMaximized;

    /// <summary>
    /// Gets the current platform.
    /// </summary>
    public HermesPlatform Platform => _backend.Platform;

    /// <summary>
    /// Minimize the window at runtime.
    /// </summary>
    public void MinimizeWindow()
    {
        EnsureInitialized();
        _backend.IsMinimized = true;
    }

    /// <summary>
    /// Restore the window from minimized state.
    /// </summary>
    public void RestoreWindow()
    {
        EnsureInitialized();
        _backend.IsMinimized = false;
    }

    /// <summary>
    /// Maximize the window at runtime.
    /// </summary>
    public void MaximizeWindow()
    {
        EnsureInitialized();
        _backend.IsMaximized = true;
    }

    /// <summary>
    /// Toggle between maximized and normal window state.
    /// </summary>
    public void ToggleMaximize()
    {
        EnsureInitialized();
        _backend.IsMaximized = !_backend.IsMaximized;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _backend.Dispose();
    }

    #endregion

    #region Internal

    /// <summary>
    /// Gets the underlying platform backend. Used by Hermes.Blazor for threading.
    /// </summary>
    internal IHermesWindowBackend Backend => _backend;

    #endregion

    #region Private Helpers

    private void EnsureInitialized()
    {
        if (_initialized) return;

        // Restore saved window state before backend initialization
        if (_options.WindowStateKey is not null)
        {
            // Resolve key: use title if empty string sentinel
            _windowStateKey = string.IsNullOrEmpty(_options.WindowStateKey)
                ? _options.Title
                : _options.WindowStateKey;

            RestoreWindowState();
        }

        _backend.Initialize(_options);

        // Subscribe to events for state persistence
        if (_windowStateKey is not null)
        {
            _backend.Closing += SaveWindowState;
            _backend.Resized += OnResizedForPersistence;
            _backend.Moved += OnMovedForPersistence;
            _backend.Maximized += OnMaximizedForPersistence;
            _backend.Restored += OnRestoredForPersistence;

            // Seed normal state from options (not backend, which may report 0x0 before Show())
            _lastNormalX = _options.X ?? 0;
            _lastNormalY = _options.Y ?? 0;
            _lastNormalWidth = _options.Width;
            _lastNormalHeight = _options.Height;
        }

        _initialized = true;
    }

    private void RestoreWindowState()
    {
        if (_windowStateKey is null)
            return;

        if (WindowStateStore.Instance.TryGetState(_windowStateKey, out var state) && state is not null)
        {
            if (state.Width < MinPersistableSize || state.Height < MinPersistableSize)
            {
                HermesLogger.Warning($"Ignoring corrupted window state for '{_windowStateKey}': size {state.Width}x{state.Height}");
                return;
            }

            _options.X = state.X;
            _options.Y = state.Y;
            _options.Width = state.Width;
            _options.Height = state.Height;
            _options.Maximized = state.IsMaximized;
            _options.CenterOnScreen = false; // Use saved position instead of centering

            HermesLogger.Info($"Restored window state for '{_windowStateKey}'");
        }
    }

    private void SaveWindowState()
    {
        if (_windowStateKey is null || !_initialized)
            return;

        try
        {
            // When maximized, save the cached normal dimensions (captured before maximize)
            // Otherwise, save the current backend dimensions
            var isMaximized = _backend.IsMaximized;
            var state = new WindowState
            {
                X = isMaximized ? _lastNormalX : _backend.Position.X,
                Y = isMaximized ? _lastNormalY : _backend.Position.Y,
                Width = isMaximized ? _lastNormalWidth : _backend.Size.Width,
                Height = isMaximized ? _lastNormalHeight : _backend.Size.Height,
                IsMaximized = isMaximized
            };

            if (state.Width < MinPersistableSize || state.Height < MinPersistableSize)
            {
                HermesLogger.Warning($"Skipping window state save for '{_windowStateKey}': degenerate size {state.Width}x{state.Height}");
                return;
            }

            WindowStateStore.Instance.SaveState(_windowStateKey, state);
            HermesLogger.Info($"Saved window state for '{_windowStateKey}'");
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Failed to save window state for '{_windowStateKey}': {ex.Message}");
        }
    }

    private void SaveWindowStateWithDimensions(int x, int y, int width, int height, bool isMaximized)
    {
        if (_windowStateKey is null || !_initialized)
            return;

        try
        {
            if (width < MinPersistableSize || height < MinPersistableSize)
            {
                HermesLogger.Warning($"Skipping window state save for '{_windowStateKey}': degenerate size {width}x{height}");
                return;
            }

            var state = new WindowState
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                IsMaximized = isMaximized
            };

            WindowStateStore.Instance.SaveState(_windowStateKey, state);
            HermesLogger.Info($"Saved window state for '{_windowStateKey}'");
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Failed to save window state for '{_windowStateKey}': {ex.Message}");
        }
    }

    private void OnResizedForPersistence(int width, int height)
    {
        if (!_backend.IsMaximized)
        {
            _lastNormalWidth = width;
            _lastNormalHeight = height;
        }
    }

    private void OnMovedForPersistence(int x, int y)
    {
        if (!_backend.IsMaximized)
        {
            _lastNormalX = x;
            _lastNormalY = y;
        }
    }

    private void OnMaximizedForPersistence()
    {
        // Save the cached normal state (captured before maximize)
        SaveWindowStateWithDimensions(_lastNormalX, _lastNormalY,
            _lastNormalWidth, _lastNormalHeight, isMaximized: true);
    }

    private void OnRestoredForPersistence()
    {
        // After restore, save current (now normal) state
        SaveWindowState();
    }

    private void ThrowIfInitialized()
    {
        if (_initialized)
            throw new InvalidOperationException("Cannot modify window options after initialization. Call configuration methods before Show() or WaitForClose().");
    }

    private static IHermesWindowBackend CreatePlatformBackend()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
            return new Platforms.Windows.WindowsWindowBackend();
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
            return new Platforms.Linux.LinuxWindowBackend();
#endif
#if MACOS
        if (OperatingSystem.IsMacOS())
            return new Platforms.macOS.MacWindowBackend();
#endif
        throw new PlatformNotSupportedException($"Hermes is not supported on this platform. Current OS: {Environment.OSVersion}");
    }

    private IMenuBackend CreatePlatformMenuBackend()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
        {
            EnsureInitialized();
            var winBackend = (Platforms.Windows.WindowsWindowBackend)_backend;
            return winBackend.CreateMenuBackend();
        }
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
        {
            EnsureInitialized();
            var linuxBackend = (Platforms.Linux.LinuxWindowBackend)_backend;
            return linuxBackend.CreateMenuBackend();
        }
#endif
#if MACOS
        if (OperatingSystem.IsMacOS())
        {
            EnsureInitialized();
            var macBackend = (Platforms.macOS.MacWindowBackend)_backend;
            return macBackend.CreateMenuBackend();
        }
#endif
        throw new PlatformNotSupportedException($"Menu backend not supported on this platform. Current OS: {Environment.OSVersion}");
    }

    private IDialogBackend CreatePlatformDialogBackend()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
        {
            EnsureInitialized();
            var winBackend = (Platforms.Windows.WindowsWindowBackend)_backend;
            return winBackend.CreateDialogBackend();
        }
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
        {
            EnsureInitialized();
            var linuxBackend = (Platforms.Linux.LinuxWindowBackend)_backend;
            return linuxBackend.CreateDialogBackend();
        }
#endif
#if MACOS
        if (OperatingSystem.IsMacOS())
        {
            EnsureInitialized();
            var macBackend = (Platforms.macOS.MacWindowBackend)_backend;
            return macBackend.CreateDialogBackend();
        }
#endif
        throw new PlatformNotSupportedException($"Dialog backend not supported on this platform. Current OS: {Environment.OSVersion}");
    }

    private IContextMenuBackend CreatePlatformContextMenuBackend()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
        {
            EnsureInitialized();
            var winBackend = (Platforms.Windows.WindowsWindowBackend)_backend;
            return winBackend.CreateContextMenuBackend();
        }
#endif
#if LINUX
        if (OperatingSystem.IsLinux())
        {
            EnsureInitialized();
            var linuxBackend = (Platforms.Linux.LinuxWindowBackend)_backend;
            return linuxBackend.CreateContextMenuBackend();
        }
#endif
#if MACOS
        if (OperatingSystem.IsMacOS())
        {
            EnsureInitialized();
            var macBackend = (Platforms.macOS.MacWindowBackend)_backend;
            return macBackend.CreateContextMenuBackend();
        }
#endif
        throw new PlatformNotSupportedException($"Context menu backend not supported on this platform. Current OS: {Environment.OSVersion}");
    }

    #endregion
}
