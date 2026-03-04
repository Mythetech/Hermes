// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.Testing;

/// <summary>
/// A test-friendly HermesWindow that uses a <see cref="RecordingWindowBackend"/> by default.
/// Provides access to the recording for verification in tests.
/// </summary>
public sealed class TestableHermesWindow : IDisposable
{
    private readonly HermesWindow _window;
    private readonly RecordingWindowBackend _backend;

    /// <summary>
    /// Create a new testable Hermes window with a recording backend.
    /// </summary>
    public TestableHermesWindow()
    {
        _backend = new RecordingWindowBackend();
        _window = new HermesWindow(_backend);
    }

    /// <summary>
    /// Create a new testable Hermes window with a custom recording backend.
    /// </summary>
    public TestableHermesWindow(RecordingWindowBackend backend)
    {
        _backend = backend;
        _window = new HermesWindow(backend);
    }

    /// <summary>
    /// The underlying HermesWindow for configuration and operations.
    /// </summary>
    public HermesWindow Window => _window;

    /// <summary>
    /// The recording backend for event simulation and verification.
    /// </summary>
    public RecordingWindowBackend Backend => _backend;

    /// <summary>
    /// The recording of all window interactions.
    /// </summary>
    public WindowBackendRecording Recording => _backend.Recording;

    #region Fluent Configuration Passthrough

    /// <summary>
    /// Set the window title.
    /// </summary>
    public TestableHermesWindow SetTitle(string title)
    {
        _window.SetTitle(title);
        return this;
    }

    /// <summary>
    /// Set the initial window size.
    /// </summary>
    public TestableHermesWindow SetSize(int width, int height)
    {
        _window.SetSize(width, height);
        return this;
    }

    /// <summary>
    /// Set the initial window position.
    /// </summary>
    public TestableHermesWindow SetPosition(int x, int y)
    {
        _window.SetPosition(x, y);
        return this;
    }

    /// <summary>
    /// Center the window on screen when shown.
    /// </summary>
    public TestableHermesWindow Center()
    {
        _window.Center();
        return this;
    }

    /// <summary>
    /// Set whether the window is resizable.
    /// </summary>
    public TestableHermesWindow SetResizable(bool resizable)
    {
        _window.SetResizable(resizable);
        return this;
    }

    /// <summary>
    /// Set whether the window is chromeless.
    /// </summary>
    public TestableHermesWindow SetChromeless(bool chromeless)
    {
        _window.SetChromeless(chromeless);
        return this;
    }

    /// <summary>
    /// Enable custom title bar mode.
    /// </summary>
    public TestableHermesWindow SetCustomTitleBar(bool enabled)
    {
        _window.SetCustomTitleBar(enabled);
        return this;
    }

    /// <summary>
    /// Enable window state persistence.
    /// </summary>
    public TestableHermesWindow RememberWindowState(string? key = null)
    {
        _window.RememberWindowState(key);
        return this;
    }

    /// <summary>
    /// Start the window maximized.
    /// </summary>
    public TestableHermesWindow Maximize()
    {
        _window.Maximize();
        return this;
    }

    /// <summary>
    /// Start the window minimized.
    /// </summary>
    public TestableHermesWindow Minimize()
    {
        _window.Minimize();
        return this;
    }

    /// <summary>
    /// Load a URL in the WebView.
    /// </summary>
    public TestableHermesWindow Load(string url)
    {
        _window.Load(url);
        return this;
    }

    /// <summary>
    /// Load HTML content directly in the WebView.
    /// </summary>
    public TestableHermesWindow LoadHtml(string html)
    {
        _window.LoadHtml(html);
        return this;
    }

    /// <summary>
    /// Register a handler for messages from JavaScript.
    /// </summary>
    public TestableHermesWindow OnWebMessage(Action<string> handler)
    {
        _window.OnWebMessage(handler);
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is maximized.
    /// </summary>
    public TestableHermesWindow OnMaximized(Action handler)
    {
        _window.OnMaximized(handler);
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is restored.
    /// </summary>
    public TestableHermesWindow OnRestored(Action handler)
    {
        _window.OnRestored(handler);
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is resized.
    /// </summary>
    public TestableHermesWindow OnResized(Action<int, int> handler)
    {
        _window.OnResized(handler);
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is moved.
    /// </summary>
    public TestableHermesWindow OnMoved(Action<int, int> handler)
    {
        _window.OnMoved(handler);
        return this;
    }

    /// <summary>
    /// Register a handler for when the window is closing.
    /// </summary>
    public TestableHermesWindow OnClosing(Action handler)
    {
        _window.OnClosing(handler);
        return this;
    }

    #endregion

    #region Lifecycle

    /// <summary>
    /// Show the window (does not block in test mode).
    /// </summary>
    public void Show()
    {
        _window.Show();
    }

    /// <summary>
    /// Show the window and wait for close (does not actually block in test mode).
    /// </summary>
    public void WaitForClose()
    {
        _window.WaitForClose();
    }

    /// <summary>
    /// Close the window.
    /// </summary>
    public void Close()
    {
        _window.Close();
    }

    #endregion

    #region Event Simulation

    /// <summary>
    /// Simulate a web message being received from JavaScript.
    /// </summary>
    public void SimulateWebMessage(string message)
    {
        _backend.SimulateWebMessage(message);
    }

    /// <summary>
    /// Simulate a window resize.
    /// </summary>
    public void SimulateResize(int width, int height)
    {
        _backend.SimulateResize(width, height);
    }

    /// <summary>
    /// Simulate a window move.
    /// </summary>
    public void SimulateMove(int x, int y)
    {
        _backend.SimulateMove(x, y);
    }

    /// <summary>
    /// Simulate the window being maximized.
    /// </summary>
    public void SimulateMaximize()
    {
        _backend.SimulateMaximize();
    }

    /// <summary>
    /// Simulate the window being restored.
    /// </summary>
    public void SimulateRestore()
    {
        _backend.SimulateRestore();
    }

    /// <summary>
    /// Simulate a click on a drag region (custom titlebar).
    /// </summary>
    public void SimulateDragRegionClick()
    {
        _backend.SimulateDragRegionClick();
    }

    /// <summary>
    /// Simulate a double-click on a drag region (custom titlebar).
    /// </summary>
    public void SimulateDragRegionDoubleClick()
    {
        _backend.SimulateDragRegionDoubleClick();
    }

    /// <summary>
    /// Simulate a click on a non-drag region (custom titlebar).
    /// </summary>
    public void SimulateNonDragRegionClick()
    {
        _backend.SimulateNonDragRegionClick();
    }

    /// <summary>
    /// Simulate the window gaining focus.
    /// </summary>
    public void SimulateFocusIn()
    {
        _backend.SimulateFocusIn();
    }

    /// <summary>
    /// Simulate the window losing focus.
    /// </summary>
    public void SimulateFocusOut()
    {
        _backend.SimulateFocusOut();
    }

    #endregion

    public void Dispose()
    {
        _window.Dispose();
    }
}
