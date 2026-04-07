// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.Testing;

/// <summary>
/// A mock status icon backend that records all operations for verification in tests.
/// </summary>
public sealed class RecordingStatusIconBackend : IStatusIconBackend
{
    private readonly List<string> _operations = new();
    private bool _disposed;

    public event Action<string>? MenuItemClicked;
    public event Action? Clicked;
    public event Action? DoubleClicked;

    /// <summary>
    /// All recorded operations in order.
    /// </summary>
    public IReadOnlyList<string> Operations => _operations;

    /// <summary>
    /// Whether the backend has been initialized.
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Whether the icon is currently visible.
    /// </summary>
    public bool IsVisible { get; private set; }

    /// <summary>
    /// The current tooltip text.
    /// </summary>
    public string? CurrentTooltip { get; private set; }

    /// <summary>
    /// The current icon path.
    /// </summary>
    public string? CurrentIconPath { get; private set; }

    /// <summary>
    /// Whether the icon was set from a stream.
    /// </summary>
    public bool IconSetFromStream { get; private set; }

    public void Initialize()
    {
        IsInitialized = true;
        _operations.Add("Initialize");
    }

    public void Show()
    {
        IsVisible = true;
        _operations.Add("Show");
    }

    public void Hide()
    {
        IsVisible = false;
        _operations.Add("Hide");
    }

    public void SetIcon(string filePath)
    {
        CurrentIconPath = filePath;
        IconSetFromStream = false;
        _operations.Add($"SetIcon:{filePath}");
    }

    public void SetIconFromStream(Stream stream)
    {
        IconSetFromStream = true;
        _operations.Add("SetIconFromStream");
    }

    public void SetTooltip(string tooltip)
    {
        CurrentTooltip = tooltip;
        _operations.Add($"SetTooltip:{tooltip}");
    }

    public void AddMenuItem(string itemId, string label)
        => _operations.Add($"AddMenuItem:{itemId}={label}");

    public void AddMenuSeparator()
        => _operations.Add("AddMenuSeparator");

    public void RemoveMenuItem(string itemId)
        => _operations.Add($"RemoveMenuItem:{itemId}");

    public void ClearMenu()
        => _operations.Add("ClearMenu");

    public void SetMenuItemEnabled(string itemId, bool enabled)
        => _operations.Add($"SetMenuItemEnabled:{itemId}={enabled}");

    public void SetMenuItemChecked(string itemId, bool isChecked)
        => _operations.Add($"SetMenuItemChecked:{itemId}={isChecked}");

    public void SetMenuItemLabel(string itemId, string label)
        => _operations.Add($"SetMenuItemLabel:{itemId}={label}");

    public void AddSubmenu(string submenuId, string label)
        => _operations.Add($"AddSubmenu:{submenuId}={label}");

    public void AddSubmenuItem(string submenuId, string itemId, string label)
        => _operations.Add($"AddSubmenuItem:{submenuId}/{itemId}={label}");

    public void AddSubmenuSeparator(string submenuId)
        => _operations.Add($"AddSubmenuSeparator:{submenuId}");

    public void ClearSubmenu(string submenuId)
        => _operations.Add($"ClearSubmenu:{submenuId}");

    /// <summary>
    /// Simulate a menu item click for testing.
    /// </summary>
    public void SimulateMenuItemClick(string itemId) => MenuItemClicked?.Invoke(itemId);

    /// <summary>
    /// Simulate a tray icon click for testing.
    /// </summary>
    public void SimulateClick() => Clicked?.Invoke();

    /// <summary>
    /// Simulate a tray icon double-click for testing.
    /// </summary>
    public void SimulateDoubleClick() => DoubleClicked?.Invoke();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operations.Add("Dispose");
    }
}
