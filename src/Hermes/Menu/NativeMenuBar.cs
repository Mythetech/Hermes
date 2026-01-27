using Hermes.Abstractions;

namespace Hermes.Menu;

/// <summary>
/// High-level fluent API for managing the native menu bar.
/// Wraps IMenuBackend and provides indexer-based item access.
/// </summary>
public sealed class NativeMenuBar
{
    private readonly IMenuBackend _backend;
    private readonly Dictionary<string, NativeMenu> _menus = new();
    private readonly Dictionary<string, NativeMenuItem> _itemsById = new();

    internal NativeMenuBar(IMenuBackend backend)
    {
        _backend = backend;
        _backend.MenuItemClicked += OnMenuItemClicked;
    }

    /// <summary>
    /// Event raised when any menu item is clicked.
    /// The parameter is the item ID.
    /// </summary>
    public event Action<string>? ItemClicked;

    /// <summary>
    /// Get a menu item by its ID (e.g., "file.save").
    /// </summary>
    /// <param name="itemId">The unique item identifier.</param>
    /// <returns>The menu item.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the item is not found.</exception>
    public NativeMenuItem this[string itemId]
    {
        get
        {
            if (_itemsById.TryGetValue(itemId, out var item))
                return item;
            throw new KeyNotFoundException($"Menu item '{itemId}' not found.");
        }
    }

    /// <summary>
    /// Try to get a menu item by ID.
    /// </summary>
    /// <param name="itemId">The item ID to look up.</param>
    /// <param name="item">The menu item if found.</param>
    /// <returns>True if the item was found, false otherwise.</returns>
    public bool TryGetItem(string itemId, out NativeMenuItem? item)
    {
        return _itemsById.TryGetValue(itemId, out item);
    }

    /// <summary>
    /// Check if a menu item exists.
    /// </summary>
    /// <param name="itemId">The item ID to check.</param>
    /// <returns>True if the item exists, false otherwise.</returns>
    public bool ContainsItem(string itemId) => _itemsById.ContainsKey(itemId);

    /// <summary>
    /// Get a menu by its label (e.g., "File", "Edit").
    /// </summary>
    /// <param name="label">The menu label.</param>
    /// <returns>The menu.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the menu is not found.</exception>
    public NativeMenu GetMenu(string label)
    {
        if (_menus.TryGetValue(label, out var menu))
            return menu;
        throw new KeyNotFoundException($"Menu '{label}' not found.");
    }

    /// <summary>
    /// Try to get a menu by label.
    /// </summary>
    /// <param name="label">The menu label to look up.</param>
    /// <param name="menu">The menu if found.</param>
    /// <returns>True if the menu was found, false otherwise.</returns>
    public bool TryGetMenu(string label, out NativeMenu? menu)
    {
        return _menus.TryGetValue(label, out menu);
    }

    /// <summary>
    /// Check if a menu exists.
    /// </summary>
    /// <param name="label">The menu label to check.</param>
    /// <returns>True if the menu exists, false otherwise.</returns>
    public bool ContainsMenu(string label) => _menus.ContainsKey(label);

    /// <summary>
    /// Add a new menu to the menu bar.
    /// </summary>
    /// <param name="label">Display label for the menu.</param>
    /// <param name="configure">Configuration callback to add items to the menu.</param>
    /// <returns>This menu bar for method chaining.</returns>
    public NativeMenuBar AddMenu(string label, Action<NativeMenu> configure)
    {
        return AddMenu(label, -1, configure);
    }

    /// <summary>
    /// Add a new menu at a specific position in the menu bar.
    /// </summary>
    /// <param name="label">Display label for the menu.</param>
    /// <param name="insertIndex">Position to insert at, or -1 to append.</param>
    /// <param name="configure">Configuration callback to add items to the menu.</param>
    /// <returns>This menu bar for method chaining.</returns>
    public NativeMenuBar AddMenu(string label, int insertIndex, Action<NativeMenu> configure)
    {
        if (_menus.ContainsKey(label))
            throw new InvalidOperationException($"Menu '{label}' already exists.");

        _backend.AddMenu(label, insertIndex);

        var menu = new NativeMenu(_backend, this, label);
        _menus[label] = menu;

        configure(menu);

        return this;
    }

    /// <summary>
    /// Remove a menu from the menu bar.
    /// </summary>
    /// <param name="label">Label of the menu to remove.</param>
    /// <returns>This menu bar for method chaining.</returns>
    public NativeMenuBar RemoveMenu(string label)
    {
        if (!_menus.TryGetValue(label, out var menu))
            return this;

        // Unregister all items in this menu
        foreach (var item in menu.Items)
        {
            _itemsById.Remove(item.Id);
        }

        _backend.RemoveMenu(label);
        _menus.Remove(label);

        return this;
    }

    /// <summary>
    /// Register an item for global lookup.
    /// </summary>
    internal void RegisterItem(NativeMenuItem item)
    {
        _itemsById[item.Id] = item;
    }

    /// <summary>
    /// Unregister an item from global lookup.
    /// </summary>
    internal void UnregisterItem(string itemId)
    {
        _itemsById.Remove(itemId);
    }

    private void OnMenuItemClicked(string itemId)
    {
        ItemClicked?.Invoke(itemId);
    }
}
