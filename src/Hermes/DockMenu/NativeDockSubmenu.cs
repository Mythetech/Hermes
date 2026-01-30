using Hermes.Abstractions;

namespace Hermes.DockMenu;

/// <summary>
/// Represents a submenu within the dock menu.
/// </summary>
public sealed class NativeDockSubmenu
{
    private readonly IDockMenuBackend _backend;
    private readonly Dictionary<string, NativeDockMenuItem> _globalItemsById;
    private readonly List<NativeDockMenuItem> _items = new();

    internal NativeDockSubmenu(
        IDockMenuBackend backend,
        string submenuId,
        string label,
        Dictionary<string, NativeDockMenuItem> globalItemsById)
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
    public IReadOnlyList<NativeDockMenuItem> Items => _items;

    /// <summary>
    /// Add an item to this submenu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This submenu for method chaining.</returns>
    public NativeDockSubmenu AddItem(string label, string itemId, Action<NativeDockMenuItem>? configure = null)
    {
        var item = new NativeDockMenuItem(_backend, itemId, label);

        // Allow configuration before registering
        configure?.Invoke(item);

        // Register with backend
        _backend.AddSubmenuItem(Id, itemId, label);

        // Apply initial state
        if (!item.IsEnabled)
            _backend.SetItemEnabled(itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(itemId, true);

        _items.Add(item);
        _globalItemsById[itemId] = item;

        return this;
    }

    /// <summary>
    /// Add a separator to this submenu.
    /// </summary>
    /// <returns>This submenu for method chaining.</returns>
    public NativeDockSubmenu AddSeparator()
    {
        _backend.AddSubmenuSeparator(Id);
        return this;
    }

    /// <summary>
    /// Clear all items from this submenu.
    /// </summary>
    /// <returns>This submenu for method chaining.</returns>
    public NativeDockSubmenu Clear()
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
