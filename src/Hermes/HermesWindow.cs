using Hermes.Abstractions;

namespace Hermes;

/// <summary>
/// A cross-platform native window with an embedded WebView.
/// Use fluent methods to configure, then call WaitForClose() to show and run.
/// </summary>
public sealed class HermesWindow : IDisposable
{
    private readonly IHermesWindowBackend _backend;
    private readonly HermesWindowOptions _options = new();
    private IMenuBackend? _menuBackend;
    private IDialogBackend? _dialogBackend;
    private bool _initialized;
    private bool _disposed;

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
    /// </summary>
    public IMenuBackend MenuBar
    {
        get
        {
            _menuBackend ??= CreatePlatformMenuBackend();
            return _menuBackend;
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
        if (OperatingSystem.IsWindows())
            throw new NotImplementedException("Windows backend not yet implemented");
        if (OperatingSystem.IsLinux())
            throw new NotImplementedException("Linux backend not yet implemented");
        if (OperatingSystem.IsMacOS())
            throw new NotImplementedException("macOS backend not yet implemented");

        throw new PlatformNotSupportedException("Hermes only supports Windows, Linux, and macOS.");
    }

    private static IMenuBackend CreatePlatformMenuBackend()
    {
        if (OperatingSystem.IsWindows())
            throw new NotImplementedException("Windows menu backend not yet implemented");
        if (OperatingSystem.IsLinux())
            throw new NotImplementedException("Linux menu backend not yet implemented");
        if (OperatingSystem.IsMacOS())
            throw new NotImplementedException("macOS menu backend not yet implemented");

        throw new PlatformNotSupportedException("Hermes only supports Windows, Linux, and macOS.");
    }

    private static IDialogBackend CreatePlatformDialogBackend()
    {
        if (OperatingSystem.IsWindows())
            throw new NotImplementedException("Windows dialog backend not yet implemented");
        if (OperatingSystem.IsLinux())
            throw new NotImplementedException("Linux dialog backend not yet implemented");
        if (OperatingSystem.IsMacOS())
            throw new NotImplementedException("macOS dialog backend not yet implemented");

        throw new PlatformNotSupportedException("Hermes only supports Windows, Linux, and macOS.");
    }

    #endregion
}
