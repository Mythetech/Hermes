// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.DockMenu;

/// <summary>
/// The application dock menu that appears when right-clicking the dock icon.
/// Provides a fluent API for building and managing dock menu items.
/// Custom items appear above the default macOS entries (Options, Show All Windows, Hide, Quit).
/// </summary>
public sealed class NativeDockMenu : IDisposable
{
    private readonly IDockMenuBackend _backend;
    private readonly Dictionary<string, NativeDockMenuItem> _itemsById = new();
    private readonly Dictionary<string, NativeDockSubmenu> _submenusById = new();
    private readonly List<object> _items = new(); // Mixed items and submenus in order
    private bool _disposed;

    internal NativeDockMenu(IDockMenuBackend backend)
    {
        _backend = backend;
        _backend.MenuItemClicked += OnItemClicked;
    }

    /// <summary>
    /// Event raised when a dock menu item is clicked.
    /// The parameter is the item ID.
    /// </summary>
    public event Action<string>? ItemClicked;

    /// <summary>
    /// Get an item by its ID.
    /// </summary>
    /// <param name="itemId">The unique item identifier.</param>
    /// <returns>The menu item.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the item is not found.</exception>
    public NativeDockMenuItem this[string itemId]
    {
        get
        {
            if (_itemsById.TryGetValue(itemId, out var item))
                return item;
            throw new KeyNotFoundException($"Dock menu item '{itemId}' not found.");
        }
    }

    /// <summary>
    /// Try to get an item by ID.
    /// </summary>
    /// <param name="itemId">The item ID to look up.</param>
    /// <param name="item">The menu item if found.</param>
    /// <returns>True if the item was found, false otherwise.</returns>
    public bool TryGetItem(string itemId, out NativeDockMenuItem? item)
    {
        return _itemsById.TryGetValue(itemId, out item);
    }

    /// <summary>
    /// Try to get a submenu by ID.
    /// </summary>
    /// <param name="submenuId">The submenu ID to look up.</param>
    /// <param name="submenu">The submenu if found.</param>
    /// <returns>True if the submenu was found, false otherwise.</returns>
    public bool TryGetSubmenu(string submenuId, out NativeDockSubmenu? submenu)
    {
        return _submenusById.TryGetValue(submenuId, out submenu);
    }

    /// <summary>
    /// Add a menu item to the dock menu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This dock menu for method chaining.</returns>
    public NativeDockMenu AddItem(string label, string itemId, Action<NativeDockMenuItem>? configure = null)
    {
        EnsureNotDisposed();

        var item = new NativeDockMenuItem(_backend, itemId, label);

        // Allow configuration before registering with backend
        configure?.Invoke(item);

        // Register with backend
        _backend.AddItem(itemId, label);

        // Apply initial state if different from defaults
        if (!item.IsEnabled)
            _backend.SetItemEnabled(itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(itemId, true);

        _items.Add(item);
        _itemsById[itemId] = item;

        return this;
    }

    /// <summary>
    /// Add a separator to the dock menu.
    /// </summary>
    /// <returns>This dock menu for method chaining.</returns>
    public NativeDockMenu AddSeparator()
    {
        EnsureNotDisposed();
        _backend.AddSeparator();
        return this;
    }

    /// <summary>
    /// Add a submenu to the dock menu.
    /// </summary>
    /// <param name="label">Display label for the submenu.</param>
    /// <param name="submenuId">Unique identifier for the submenu.</param>
    /// <param name="configure">Optional configuration callback for the submenu.</param>
    /// <returns>This dock menu for method chaining.</returns>
    public NativeDockMenu AddSubmenu(string label, string submenuId, Action<NativeDockSubmenu>? configure = null)
    {
        EnsureNotDisposed();

        var submenu = new NativeDockSubmenu(_backend, submenuId, label, _itemsById);

        // Register with backend
        _backend.AddSubmenu(submenuId, label);

        // Allow configuration
        configure?.Invoke(submenu);

        _items.Add(submenu);
        _submenusById[submenuId] = submenu;

        return this;
    }

    /// <summary>
    /// Remove an item from the dock menu.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    /// <returns>This dock menu for method chaining.</returns>
    public NativeDockMenu RemoveItem(string itemId)
    {
        EnsureNotDisposed();

        if (!_itemsById.TryGetValue(itemId, out var item))
            return this;

        _backend.RemoveItem(itemId);
        _items.Remove(item);
        _itemsById.Remove(itemId);

        return this;
    }

    /// <summary>
    /// Remove all items from the dock menu.
    /// </summary>
    /// <returns>This dock menu for method chaining.</returns>
    public NativeDockMenu Clear()
    {
        EnsureNotDisposed();

        _backend.Clear();
        _items.Clear();
        _itemsById.Clear();
        _submenusById.Clear();

        return this;
    }

    private void OnItemClicked(string itemId)
    {
        ItemClicked?.Invoke(itemId);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NativeDockMenu));
    }

    /// <summary>
    /// Dispose of the dock menu and release native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _backend.MenuItemClicked -= OnItemClicked;
        _backend.Dispose();
    }
}
