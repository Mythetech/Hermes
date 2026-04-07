// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.StatusIcon;

/// <summary>
/// The tray context menu that appears when interacting with the system tray icon.
/// Provides a fluent API for building and managing tray menu items.
/// </summary>
public sealed class NativeTrayMenu
{
    private readonly IStatusIconBackend _backend;
    private readonly Dictionary<string, NativeTrayMenuItem> _itemsById = new();
    private readonly Dictionary<string, NativeTraySubmenu> _submenusById = new();
    private readonly List<object> _items = new(); // Mixed items and submenus in order

    internal NativeTrayMenu(IStatusIconBackend backend)
    {
        _backend = backend;
        _backend.MenuItemClicked += OnItemClicked;
    }

    /// <summary>
    /// Event raised when a tray menu item is clicked.
    /// The parameter is the item ID.
    /// </summary>
    public event Action<string>? ItemClicked;

    /// <summary>
    /// Get an item by its ID.
    /// </summary>
    /// <param name="itemId">The unique item identifier.</param>
    /// <returns>The menu item.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the item is not found.</exception>
    public NativeTrayMenuItem this[string itemId]
    {
        get
        {
            if (_itemsById.TryGetValue(itemId, out var item))
                return item;
            throw new KeyNotFoundException($"Tray menu item '{itemId}' not found.");
        }
    }

    /// <summary>
    /// Try to get an item by ID.
    /// </summary>
    /// <param name="itemId">The item ID to look up.</param>
    /// <param name="item">The menu item if found.</param>
    /// <returns>True if the item was found, false otherwise.</returns>
    public bool TryGetItem(string itemId, out NativeTrayMenuItem? item)
    {
        return _itemsById.TryGetValue(itemId, out item);
    }

    /// <summary>
    /// Try to get a submenu by ID.
    /// </summary>
    /// <param name="submenuId">The submenu ID to look up.</param>
    /// <param name="submenu">The submenu if found.</param>
    /// <returns>True if the submenu was found, false otherwise.</returns>
    public bool TryGetSubmenu(string submenuId, out NativeTraySubmenu? submenu)
    {
        return _submenusById.TryGetValue(submenuId, out submenu);
    }

    /// <summary>
    /// Add a menu item to the tray context menu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This tray menu for method chaining.</returns>
    public NativeTrayMenu AddItem(string label, string itemId, Action<NativeTrayMenuItem>? configure = null)
    {
        var item = new NativeTrayMenuItem(_backend, itemId, label);

        // Allow configuration before registering with backend
        configure?.Invoke(item);

        // Register with backend
        _backend.AddMenuItem(itemId, label);

        // Apply initial state if different from defaults
        if (!item.IsEnabled)
            _backend.SetMenuItemEnabled(itemId, false);
        if (item.IsChecked)
            _backend.SetMenuItemChecked(itemId, true);

        _items.Add(item);
        _itemsById[itemId] = item;

        return this;
    }

    /// <summary>
    /// Add a separator to the tray context menu.
    /// </summary>
    /// <returns>This tray menu for method chaining.</returns>
    public NativeTrayMenu AddSeparator()
    {
        _backend.AddMenuSeparator();
        return this;
    }

    /// <summary>
    /// Add a submenu to the tray context menu.
    /// </summary>
    /// <param name="label">Display label for the submenu.</param>
    /// <param name="submenuId">Unique identifier for the submenu.</param>
    /// <param name="configure">Optional configuration callback for the submenu.</param>
    /// <returns>This tray menu for method chaining.</returns>
    public NativeTrayMenu AddSubmenu(string label, string submenuId, Action<NativeTraySubmenu>? configure = null)
    {
        var submenu = new NativeTraySubmenu(_backend, submenuId, label, _itemsById);

        // Register with backend
        _backend.AddSubmenu(submenuId, label);

        // Allow configuration
        configure?.Invoke(submenu);

        _items.Add(submenu);
        _submenusById[submenuId] = submenu;

        return this;
    }

    /// <summary>
    /// Remove an item from the tray context menu.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    /// <returns>This tray menu for method chaining.</returns>
    public NativeTrayMenu RemoveItem(string itemId)
    {
        if (!_itemsById.TryGetValue(itemId, out var item))
            return this;

        _backend.RemoveMenuItem(itemId);
        _items.Remove(item);
        _itemsById.Remove(itemId);

        return this;
    }

    /// <summary>
    /// Remove all items from the tray context menu.
    /// </summary>
    /// <returns>This tray menu for method chaining.</returns>
    public NativeTrayMenu Clear()
    {
        _backend.ClearMenu();
        _items.Clear();
        _itemsById.Clear();
        _submenusById.Clear();

        return this;
    }

    private void OnItemClicked(string itemId)
    {
        ItemClicked?.Invoke(itemId);
    }

    /// <summary>
    /// Unsubscribe from backend events. Called by NativeStatusIcon on dispose.
    /// </summary>
    internal void Detach()
    {
        _backend.MenuItemClicked -= OnItemClicked;
    }
}
