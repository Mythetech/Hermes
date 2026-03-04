// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific backend for context menu (popup menu) operations.
/// </summary>
public interface IContextMenuBackend : IDisposable
{
    #region Menu Item Operations

    /// <summary>
    /// Add a menu item to the context menu.
    /// </summary>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="label">Display label for the item.</param>
    /// <param name="accelerator">Keyboard shortcut hint (display only), or null.</param>
    void AddItem(string itemId, string label, string? accelerator = null);

    /// <summary>
    /// Add a separator to the context menu.
    /// </summary>
    void AddSeparator();

    /// <summary>
    /// Remove a menu item.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    void RemoveItem(string itemId);

    /// <summary>
    /// Remove all items from the context menu.
    /// </summary>
    void Clear();

    #endregion

    #region Item State

    /// <summary>
    /// Enable or disable a menu item.
    /// </summary>
    void SetItemEnabled(string itemId, bool enabled);

    /// <summary>
    /// Set the checked state of a menu item.
    /// </summary>
    void SetItemChecked(string itemId, bool isChecked);

    /// <summary>
    /// Update the label of a menu item.
    /// </summary>
    void SetItemLabel(string itemId, string label);

    #endregion

    #region Display

    /// <summary>
    /// Show the context menu at the specified screen coordinates.
    /// </summary>
    /// <param name="x">X coordinate in screen pixels.</param>
    /// <param name="y">Y coordinate in screen pixels.</param>
    void Show(int x, int y);

    /// <summary>
    /// Hide the context menu if currently visible.
    /// </summary>
    void Hide();

    #endregion

    #region Events

    /// <summary>
    /// Raised when a menu item is clicked. The parameter is the item ID.
    /// </summary>
    event Action<string>? MenuItemClicked;

    #endregion
}
