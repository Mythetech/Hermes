// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Abstractions;

namespace Hermes.Menu;

/// <summary>
/// Represents the application menu. On macOS, this is the system app menu
/// (containing About, Preferences, Quit). On Windows/Linux, this is a
/// top-level menu named after the application.
/// </summary>
public sealed class NativeAppMenu
{
    private readonly IMenuBackend _backend;
    private readonly NativeMenuBar _menuBar;
    private readonly List<NativeMenuItem> _items = new();

    internal NativeAppMenu(IMenuBackend backend, NativeMenuBar menuBar)
    {
        _backend = backend;
        _menuBar = menuBar;
    }

    /// <summary>
    /// All items in this app menu (excluding system items like Quit).
    /// </summary>
    public IReadOnlyList<NativeMenuItem> Items => _items;

    /// <summary>
    /// Add an item to the app menu.
    /// </summary>
    /// <param name="label">Display label (e.g., "About MyApp").</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <param name="position">Position hint for where to insert the item.</param>
    /// <returns>This app menu for method chaining.</returns>
    public NativeAppMenu AddItem(string label, string itemId, Action<NativeMenuItem>? configure = null, string? position = null)
    {
        // Create item with the app menu label marker
        var item = new NativeMenuItem(_backend, NativeMenuBar.AppMenuLabel, itemId, label);

        // Allow configuration before registering with backend
        configure?.Invoke(item);

        // Register with backend
        _backend.AddAppMenuItem(itemId, label, item.Accelerator?.ToPlatformString(), position);

        // Apply initial state if different from defaults
        if (!item.IsEnabled)
            _backend.SetItemEnabled(NativeMenuBar.AppMenuLabel, itemId, false);
        if (item.IsChecked)
            _backend.SetItemChecked(NativeMenuBar.AppMenuLabel, itemId, true);

        _items.Add(item);
        _menuBar.RegisterItem(item);

        return this;
    }

    /// <summary>
    /// Add a separator to the app menu.
    /// </summary>
    /// <param name="position">Position hint for where to insert the separator.</param>
    /// <returns>This app menu for method chaining.</returns>
    public NativeAppMenu AddSeparator(string? position = null)
    {
        _backend.AddAppMenuSeparator(position);
        return this;
    }

    /// <summary>
    /// Remove an item from the app menu.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    /// <returns>This app menu for method chaining.</returns>
    public NativeAppMenu RemoveItem(string itemId)
    {
        var item = _items.Find(i => i.Id == itemId);
        if (item is null)
            return this;

        _backend.RemoveAppMenuItem(itemId);
        _items.Remove(item);
        _menuBar.UnregisterItem(itemId);

        return this;
    }
}
