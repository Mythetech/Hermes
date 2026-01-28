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
    private readonly List<NativeMenu> _submenus = new();
    private readonly string? _parentPath;

    internal NativeMenu(IMenuBackend backend, NativeMenuBar menuBar, string label)
    {
        _backend = backend;
        _menuBar = menuBar;
        Label = label;
        _parentPath = null;
    }

    internal NativeMenu(IMenuBackend backend, NativeMenuBar menuBar, string label, string parentPath)
    {
        _backend = backend;
        _menuBar = menuBar;
        Label = label;
        _parentPath = parentPath;
    }

    /// <summary>
    /// The display label for this menu.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The full path to this menu (e.g., "File" or "File/New").
    /// </summary>
    public string Path => _parentPath is null ? Label : $"{_parentPath}/{Label}";

    /// <summary>
    /// Whether this is a submenu (has a parent).
    /// </summary>
    public bool IsSubmenu => _parentPath is not null;

    /// <summary>
    /// All submenus in this menu.
    /// </summary>
    public IReadOnlyList<NativeMenu> Submenus => _submenus;

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
    /// Add a submenu to this menu.
    /// </summary>
    /// <param name="label">Display label for the submenu.</param>
    /// <param name="configure">Configuration callback to add items to the submenu.</param>
    /// <returns>This menu for method chaining.</returns>
    public NativeMenu AddSubmenu(string label, Action<NativeMenu> configure)
    {
        _backend.AddSubmenu(Path, label);

        var submenu = new NativeMenu(_backend, _menuBar, label, Path);
        _submenus.Add(submenu);

        configure(submenu);

        return this;
    }

    /// <summary>
    /// Add a menu item to the end of this menu.
    /// </summary>
    /// <param name="label">Display label for the item.</param>
    /// <param name="itemId">Unique identifier for the item (e.g., "file.save").</param>
    /// <param name="configure">Optional configuration callback for the item.</param>
    /// <returns>This menu for method chaining.</returns>
    public NativeMenu AddItem(string label, string itemId, Action<NativeMenuItem>? configure = null)
    {
        var item = new NativeMenuItem(_backend, Path, itemId, label);
        configure?.Invoke(item);

        if (IsSubmenu)
            _backend.AddSubmenuItem(Path, itemId, label, item.Accelerator?.ToPlatformString());
        else
            _backend.AddItem(Path, itemId, label, item.Accelerator?.ToPlatformString());

        if (!item.IsEnabled)
            _backend.SetItemEnabled(Path, itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(Path, itemId, true);

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
        if (IsSubmenu)
            _backend.AddSubmenuSeparator(Path);
        else
            _backend.AddSeparator(Path);
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
        var item = new NativeMenuItem(_backend, Path, itemId, label);
        configure?.Invoke(item);

        _backend.InsertItem(Path, afterId, itemId, label, item.Accelerator?.ToPlatformString());

        if (!item.IsEnabled)
            _backend.SetItemEnabled(Path, itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(Path, itemId, true);

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

        _backend.RemoveItem(Path, itemId);
        _items.Remove(item);
        _menuBar.UnregisterItem(itemId);

        return this;
    }
}
