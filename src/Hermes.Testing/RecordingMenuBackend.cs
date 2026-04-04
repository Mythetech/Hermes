// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.Testing;

/// <summary>
/// A mock menu backend that records all operations for verification in tests.
/// Tracks which menus and items exist to mirror real backend behavior.
/// </summary>
public sealed class RecordingMenuBackend : IMenuBackend
{
    private readonly HashSet<string> _menus = new();
    private readonly List<string> _operations = new();

    public event Action<string>? MenuItemClicked;

    /// <summary>
    /// All recorded operations in order.
    /// </summary>
    public IReadOnlyList<string> Operations => _operations;

    /// <summary>
    /// The set of currently active top-level menu labels.
    /// </summary>
    public IReadOnlyCollection<string> ActiveMenus => _menus;

    /// <summary>
    /// Number of times AddMenu was called (including re-adds after remove).
    /// </summary>
    public int AddMenuCallCount { get; private set; }

    /// <summary>
    /// Number of times RemoveMenu was called.
    /// </summary>
    public int RemoveMenuCallCount { get; private set; }

    public string AppName => "TestApp";

    public void AddMenu(string label, int insertIndex = -1)
    {
        AddMenuCallCount++;
        _menus.Add(label);
        _operations.Add($"AddMenu:{label}");
    }

    public void RemoveMenu(string label)
    {
        RemoveMenuCallCount++;
        _menus.Remove(label);
        _operations.Add($"RemoveMenu:{label}");
    }

    public void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null)
        => _operations.Add($"AddItem:{menuLabel}/{itemId}");

    public void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null)
        => _operations.Add($"InsertItem:{menuLabel}/{itemId}");

    public void RemoveItem(string menuLabel, string itemId)
        => _operations.Add($"RemoveItem:{menuLabel}/{itemId}");

    public void AddSeparator(string menuLabel)
        => _operations.Add($"AddSeparator:{menuLabel}");

    public void SetItemEnabled(string menuLabel, string itemId, bool enabled)
        => _operations.Add($"SetItemEnabled:{menuLabel}/{itemId}={enabled}");

    public void SetItemChecked(string menuLabel, string itemId, bool isChecked)
        => _operations.Add($"SetItemChecked:{menuLabel}/{itemId}={isChecked}");

    public void SetItemLabel(string menuLabel, string itemId, string label)
        => _operations.Add($"SetItemLabel:{menuLabel}/{itemId}={label}");

    public void SetItemAccelerator(string menuLabel, string itemId, string accelerator)
        => _operations.Add($"SetItemAccelerator:{menuLabel}/{itemId}={accelerator}");

    public void AddSubmenu(string menuPath, string submenuLabel)
        => _operations.Add($"AddSubmenu:{menuPath}/{submenuLabel}");

    public void AddSubmenuItem(string menuPath, string itemId, string itemLabel, string? accelerator = null)
        => _operations.Add($"AddSubmenuItem:{menuPath}/{itemId}");

    public void AddSubmenuSeparator(string menuPath)
        => _operations.Add($"AddSubmenuSeparator:{menuPath}");

    public void AddAppMenuItem(string itemId, string itemLabel, string? accelerator = null, string? position = null)
        => _operations.Add($"AddAppMenuItem:{itemId}");

    public void AddAppMenuSeparator(string? position = null)
        => _operations.Add($"AddAppMenuSeparator");

    public void RemoveAppMenuItem(string itemId)
        => _operations.Add($"RemoveAppMenuItem:{itemId}");

    /// <summary>
    /// Simulate a menu item click for testing event handlers.
    /// </summary>
    public void SimulateClick(string itemId) => MenuItemClicked?.Invoke(itemId);
}
