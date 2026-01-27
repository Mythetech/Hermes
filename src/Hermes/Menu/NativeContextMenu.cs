using Hermes.Abstractions;

namespace Hermes.Menu;

/// <summary>
/// A popup context menu that can be displayed at a specific position.
/// Provides a fluent API for building and managing context menu items.
/// </summary>
public sealed class NativeContextMenu : IDisposable
{
    private readonly IContextMenuBackend _backend;
    private readonly Dictionary<string, NativeMenuItem> _itemsById = new();
    private readonly List<NativeMenuItem> _items = new();
    private bool _disposed;

    internal NativeContextMenu(IContextMenuBackend backend)
    {
        _backend = backend;
        _backend.MenuItemClicked += OnItemClicked;
    }

    /// <summary>
    /// Event raised when a context menu item is clicked.
    /// The parameter is the item ID.
    /// </summary>
    public event Action<string>? ItemClicked;

    /// <summary>
    /// Get an item by its ID.
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
            throw new KeyNotFoundException($"Context menu item '{itemId}' not found.");
        }
    }

    /// <summary>
    /// Try to get an item by ID.
    /// </summary>
    /// <param name="itemId">The item ID to look up.</param>
    /// <param name="item">The menu item if found.</param>
    /// <returns>True if the item was found, false otherwise.</returns>
    public bool TryGetItem(string itemId, out NativeMenuItem? item)
    {
        return _itemsById.TryGetValue(itemId, out item);
    }

    /// <summary>
    /// All items in this context menu.
    /// </summary>
    public IReadOnlyList<NativeMenuItem> Items => _items;

    /// <summary>
    /// Add a menu item to the context menu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This context menu for method chaining.</returns>
    public NativeContextMenu AddItem(string label, string itemId, Action<NativeMenuItem>? configure = null)
    {
        EnsureNotDisposed();

        var item = new NativeMenuItem(_backend, itemId, label);

        // Allow configuration before registering with backend
        configure?.Invoke(item);

        // Register with backend
        _backend.AddItem(itemId, label, item.Accelerator?.ToPlatformString());

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
    /// Add a separator to the context menu.
    /// </summary>
    /// <returns>This context menu for method chaining.</returns>
    public NativeContextMenu AddSeparator()
    {
        EnsureNotDisposed();
        _backend.AddSeparator();
        return this;
    }

    /// <summary>
    /// Remove an item from the context menu.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    /// <returns>This context menu for method chaining.</returns>
    public NativeContextMenu RemoveItem(string itemId)
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
    /// Remove all items from the context menu.
    /// </summary>
    /// <returns>This context menu for method chaining.</returns>
    public NativeContextMenu Clear()
    {
        EnsureNotDisposed();

        _backend.Clear();
        _items.Clear();
        _itemsById.Clear();

        return this;
    }

    /// <summary>
    /// Show the context menu at the specified screen coordinates.
    /// </summary>
    /// <param name="x">X coordinate in screen pixels.</param>
    /// <param name="y">Y coordinate in screen pixels.</param>
    public void Show(int x, int y)
    {
        EnsureNotDisposed();
        _backend.Show(x, y);
    }

    /// <summary>
    /// Hide the context menu if visible.
    /// </summary>
    public void Hide()
    {
        EnsureNotDisposed();
        _backend.Hide();
    }

    private void OnItemClicked(string itemId)
    {
        ItemClicked?.Invoke(itemId);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(NativeContextMenu));
    }

    /// <summary>
    /// Dispose of the context menu and release native resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _backend.MenuItemClicked -= OnItemClicked;
        _backend.Dispose();
    }
}
