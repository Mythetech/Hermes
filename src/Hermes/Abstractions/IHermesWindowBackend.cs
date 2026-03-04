// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Abstractions;

/// <summary>
/// Identifies the current operating system platform.
/// </summary>
public enum HermesPlatform
{
    Windows,
    macOS,
    Linux
}

/// <summary>
/// Platform-specific backend for window lifecycle and WebView management.
/// Implementations exist for Windows, Linux, and macOS.
/// </summary>
public interface IHermesWindowBackend : IDisposable
{
    #region Lifecycle

    /// <summary>
    /// Initialize the native window with the specified options.
    /// Must be called before Show() or WaitForClose().
    /// </summary>
    void Initialize(HermesWindowOptions options);

    /// <summary>
    /// Show the window without blocking.
    /// </summary>
    void Show();

    /// <summary>
    /// Close the window and release resources.
    /// </summary>
    void Close();

    /// <summary>
    /// Show the window and block until it is closed.
    /// Runs the platform message loop.
    /// </summary>
    void WaitForClose();

    #endregion

    #region Window Properties

    /// <summary>
    /// Get or set the window title.
    /// </summary>
    string Title { get; set; }

    /// <summary>
    /// Get or set the window size in pixels.
    /// </summary>
    (int Width, int Height) Size { get; set; }

    /// <summary>
    /// Get or set the window position in screen coordinates.
    /// </summary>
    (int X, int Y) Position { get; set; }

    /// <summary>
    /// Get or set whether the window is maximized.
    /// </summary>
    bool IsMaximized { get; set; }

    /// <summary>
    /// Get or set whether the window is minimized.
    /// </summary>
    bool IsMinimized { get; set; }

    /// <summary>
    /// Gets the current platform.
    /// </summary>
    HermesPlatform Platform { get; }

    #endregion

    #region WebView

    /// <summary>
    /// Navigate the WebView to the specified URL.
    /// </summary>
    void NavigateToUrl(string url);

    /// <summary>
    /// Load HTML content directly into the WebView.
    /// </summary>
    void NavigateToString(string html);

    /// <summary>
    /// Send a message to JavaScript running in the WebView.
    /// </summary>
    void SendWebMessage(string message);

    /// <summary>
    /// Register a custom URL scheme handler.
    /// The handler receives the URL and returns (content stream, content type), or (null, null) for 404.
    /// </summary>
    void RegisterCustomScheme(string scheme, Func<string, (Stream? Content, string? ContentType)> handler);

    #endregion

    #region Threading

    /// <summary>
    /// Gets the managed thread ID of the UI thread.
    /// Used by Blazor's SynchronizationContext to avoid reflection.
    /// </summary>
    int UIThreadId { get; }

    /// <summary>
    /// Returns true if the calling thread is the UI thread.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Execute an action on the UI thread synchronously.
    /// If already on the UI thread, executes immediately.
    /// </summary>
    void Invoke(Action action);

    /// <summary>
    /// Execute an action on the UI thread asynchronously.
    /// Returns immediately without waiting for execution.
    /// </summary>
    void BeginInvoke(Action action);

    #endregion

    #region Events

    /// <summary>
    /// Raised when the window is about to close.
    /// </summary>
    event Action? Closing;

    /// <summary>
    /// Raised when the window is resized. Parameters are (width, height).
    /// </summary>
    event Action<int, int>? Resized;

    /// <summary>
    /// Raised when the window is moved. Parameters are (x, y).
    /// </summary>
    event Action<int, int>? Moved;

    /// <summary>
    /// Raised when the window gains focus.
    /// </summary>
    event Action? FocusIn;

    /// <summary>
    /// Raised when the window loses focus.
    /// </summary>
    event Action? FocusOut;

    /// <summary>
    /// Raised when JavaScript sends a message via window.external.sendMessage().
    /// </summary>
    event Action<string>? WebMessageReceived;

    /// <summary>
    /// Raised when the window is maximized.
    /// </summary>
    event Action? Maximized;

    /// <summary>
    /// Raised when the window is restored from maximized state.
    /// </summary>
    event Action? Restored;

    #endregion
}
