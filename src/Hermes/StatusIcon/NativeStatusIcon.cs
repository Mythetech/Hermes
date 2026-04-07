// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.StatusIcon;

/// <summary>
/// Represents a native system tray icon. Configure via fluent methods, then call <see cref="Show"/> to display.
/// </summary>
public sealed class NativeStatusIcon : IDisposable
{
    private readonly IStatusIconBackend _backend;
    private NativeTrayMenu? _menu;
    private string? _tooltip;
    private bool _initialized;
    private bool _visible;
    private bool _disposed;

    internal NativeStatusIcon(IStatusIconBackend backend)
    {
        _backend = backend;
    }

    /// <summary>
    /// The tray context menu, if one has been configured via <see cref="SetMenu"/>.
    /// </summary>
    public NativeTrayMenu? Menu => _menu;

    /// <summary>
    /// The tooltip text displayed on hover.
    /// Setting after <see cref="Show"/> has been called will update the backend immediately.
    /// </summary>
    public string? Tooltip
    {
        get => _tooltip;
        set
        {
            _tooltip = value;
            if (_initialized && value is not null)
                _backend.SetTooltip(value);
        }
    }

    /// <summary>
    /// Whether the status icon is currently visible.
    /// Setting to false calls <see cref="Hide"/>; setting to true calls <see cref="Show"/>.
    /// </summary>
    public bool IsVisible
    {
        get => _visible;
        set
        {
            if (value)
                Show();
            else
                Hide();
        }
    }

    /// <summary>
    /// Set the icon from a file path (.png, .ico).
    /// </summary>
    /// <param name="filePath">Path to the icon file.</param>
    /// <returns>This instance for method chaining.</returns>
    public NativeStatusIcon SetIcon(string filePath)
    {
        _backend.SetIcon(filePath);
        return this;
    }

    /// <summary>
    /// Set the icon from a stream (embedded resource or memory).
    /// </summary>
    /// <param name="stream">Stream containing icon data.</param>
    /// <returns>This instance for method chaining.</returns>
    public NativeStatusIcon SetIconFromStream(Stream stream)
    {
        _backend.SetIconFromStream(stream);
        return this;
    }

    /// <summary>
    /// Set the tooltip text displayed on hover.
    /// </summary>
    /// <param name="tooltip">Tooltip text.</param>
    /// <returns>This instance for method chaining.</returns>
    public NativeStatusIcon SetTooltip(string tooltip)
    {
        _tooltip = tooltip;
        _backend.SetTooltip(tooltip);
        return this;
    }

    /// <summary>
    /// Configure the tray context menu.
    /// </summary>
    /// <param name="configure">Callback to configure the menu.</param>
    /// <returns>This instance for method chaining.</returns>
    public NativeStatusIcon SetMenu(Action<NativeTrayMenu> configure)
    {
        _menu ??= new NativeTrayMenu(_backend);
        configure(_menu);
        return this;
    }

    /// <summary>
    /// Register a handler for tray icon left-click events.
    /// </summary>
    /// <param name="handler">Handler to invoke on click.</param>
    /// <returns>This instance for method chaining.</returns>
    public NativeStatusIcon OnClicked(Action handler)
    {
        _backend.Clicked += handler;
        return this;
    }

    /// <summary>
    /// Register a handler for tray icon double-click events.
    /// </summary>
    /// <param name="handler">Handler to invoke on double-click.</param>
    /// <returns>This instance for method chaining.</returns>
    public NativeStatusIcon OnDoubleClicked(Action handler)
    {
        _backend.DoubleClicked += handler;
        return this;
    }

    /// <summary>
    /// Show the status icon in the system tray. Initializes on first call.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_initialized)
        {
            _backend.Initialize();
            _initialized = true;
        }

        _visible = true;
        _backend.Show();
    }

    /// <summary>
    /// Hide the status icon from the system tray without disposing.
    /// </summary>
    public void Hide()
    {
        if (!_initialized || _disposed)
            return;

        _visible = false;
        _backend.Hide();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _visible = false;

        if (_initialized)
            _backend.Hide();

        _menu?.Detach();
        _backend.Dispose();
    }
}
