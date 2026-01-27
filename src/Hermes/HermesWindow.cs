using Hermes.Abstractions;
using Hermes.Menu;

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
    private bool _initialized;
    private bool _disposed;

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
    public HermesWindow RegisterCustomScheme(string scheme, Func<string, Stream?> handler)
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
        _backend.Initialize(_options);
        _initialized = true;
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
