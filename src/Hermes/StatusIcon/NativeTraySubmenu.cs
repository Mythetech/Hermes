// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.StatusIcon;

/// <summary>
/// Represents a submenu within the tray context menu.
/// </summary>
public sealed class NativeTraySubmenu
{
    private readonly IStatusIconBackend _backend;
    private readonly Dictionary<string, NativeTrayMenuItem> _globalItemsById;
    private readonly List<NativeTrayMenuItem> _items = new();

    internal NativeTraySubmenu(
        IStatusIconBackend backend,
        string submenuId,
        string label,
        Dictionary<string, NativeTrayMenuItem> globalItemsById)
    {
        _backend = backend;
        _globalItemsById = globalItemsById;
        Id = submenuId;
        Label = label;
    }

    /// <summary>
    /// The unique identifier for this submenu.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// The display label for this submenu.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Items in this submenu.
    /// </summary>
    public IReadOnlyList<NativeTrayMenuItem> Items => _items;

    /// <summary>
    /// Add an item to this submenu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This submenu for method chaining.</returns>
    public NativeTraySubmenu AddItem(string label, string itemId, Action<NativeTrayMenuItem>? configure = null)
    {
        var item = new NativeTrayMenuItem(_backend, itemId, label);

        // Allow configuration before registering
        configure?.Invoke(item);

        // Register with backend
        _backend.AddSubmenuItem(Id, itemId, label);

        // Apply initial state
        if (!item.IsEnabled)
            _backend.SetMenuItemEnabled(itemId, false);
        if (item.IsChecked)
            _backend.SetMenuItemChecked(itemId, true);

        _items.Add(item);
        _globalItemsById[itemId] = item;

        return this;
    }

    /// <summary>
    /// Add a separator to this submenu.
    /// </summary>
    /// <returns>This submenu for method chaining.</returns>
    public NativeTraySubmenu AddSeparator()
    {
        _backend.AddSubmenuSeparator(Id);
        return this;
    }

    /// <summary>
    /// Clear all items from this submenu.
    /// </summary>
    /// <returns>This submenu for method chaining.</returns>
    public NativeTraySubmenu Clear()
    {
        _backend.ClearSubmenu(Id);

        // Remove items from global registry
        foreach (var item in _items)
        {
            _globalItemsById.Remove(item.Id);
        }

        _items.Clear();
        return this;
    }
}
