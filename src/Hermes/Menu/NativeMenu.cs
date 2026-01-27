using Hermes.Abstractions;

namespace Hermes.Menu;

/// <summary>
/// Represents a top-level menu in the menu bar (e.g., File, Edit, View).
/// Provides a fluent API for adding and managing menu items.
/// </summary>
public sealed class NativeMenu
{
    private readonly IMenuBackend _backend;
    private readonly NativeMenuBar _menuBar;
    private readonly List<NativeMenuItem> _items = new();

    internal NativeMenu(IMenuBackend backend, NativeMenuBar menuBar, string label)
    {
        _backend = backend;
        _menuBar = menuBar;
        Label = label;
    }

    /// <summary>
    /// The display label for this menu.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// All items in this menu.
    /// </summary>
    public IReadOnlyList<NativeMenuItem> Items => _items;

    /// <summary>
    /// Get an item by its ID.
    /// </summary>
    /// <param name="itemId">The unique item identifier.</param>
    /// <returns>The menu item, or null if not found.</returns>
    public NativeMenuItem? this[string itemId] =>
        _items.Find(i => i.Id == itemId);

    /// <summary>
    /// Add a menu item to the end of this menu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item (e.g., "file.save").</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This menu for method chaining.</returns>
    public NativeMenu AddItem(string label, string itemId, Action<NativeMenuItem>? configure = null)
    {
        var item = new NativeMenuItem(_backend, Label, itemId, label);

        // Allow configuration before registering with backend
        configure?.Invoke(item);

        // Register with backend
        _backend.AddItem(Label, itemId, label, item.Accelerator?.ToPlatformString());

        // Apply initial state if different from defaults
        if (!item.IsEnabled)
            _backend.SetItemEnabled(Label, itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(Label, itemId, true);

        _items.Add(item);
        _menuBar.RegisterItem(item);

        return this;
    }

    /// <summary>
    /// Add a separator to this menu.
    /// </summary>
    /// <returns>This menu for method chaining.</returns>
    public NativeMenu AddSeparator()
    {
        _backend.AddSeparator(Label);
        return this;
    }

    /// <summary>
    /// Insert a menu item after an existing item.
    /// </summary>
    /// <param name="afterId">ID of the item to insert after.</param>
    /// <param name="label">Display label for the new item.</param>
    /// <param name="itemId">Unique identifier for the new item.</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This menu for method chaining.</returns>
    public NativeMenu InsertItem(string afterId, string label, string itemId, Action<NativeMenuItem>? configure = null)
    {
        var item = new NativeMenuItem(_backend, Label, itemId, label);

        // Allow configuration before registering with backend
        configure?.Invoke(item);

        // Register with backend
        _backend.InsertItem(Label, afterId, itemId, label, item.Accelerator?.ToPlatformString());

        // Apply initial state if different from defaults
        if (!item.IsEnabled)
            _backend.SetItemEnabled(Label, itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(Label, itemId, true);

        // Insert in local list after the target item
        var afterIndex = _items.FindIndex(i => i.Id == afterId);
        if (afterIndex >= 0)
            _items.Insert(afterIndex + 1, item);
        else
            _items.Add(item);

        _menuBar.RegisterItem(item);

        return this;
    }

    /// <summary>
    /// Remove an item from this menu.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    /// <returns>This menu for method chaining.</returns>
    public NativeMenu RemoveItem(string itemId)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
            return this;

        _backend.RemoveItem(Label, itemId);
        _items.Remove(item);
        _menuBar.UnregisterItem(itemId);

        return this;
    }
}
