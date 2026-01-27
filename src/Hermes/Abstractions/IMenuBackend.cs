namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific backend for native menu bar operations.
/// Supports runtime modifications for dynamic plugin loading.
/// </summary>
public interface IMenuBackend
{
    #region Menu Bar Operations

    /// <summary>
    /// Add a new top-level menu to the menu bar.
    /// </summary>
    /// <param name="label">Display label for the menu.</param>
    /// <param name="insertIndex">Position to insert at, or -1 to append.</param>
    void AddMenu(string label, int insertIndex = -1);

    /// <summary>
    /// Remove a top-level menu from the menu bar.
    /// </summary>
    /// <param name="label">Label of the menu to remove.</param>
    void RemoveMenu(string label);

    #endregion

    #region Menu Item Operations

    /// <summary>
    /// Add a menu item to the end of a menu.
    /// </summary>
    /// <param name="menuLabel">Label of the parent menu.</param>
    /// <param name="itemId">Unique identifier for the item (e.g., "file.save").</param>
    /// <param name="itemLabel">Display label for the item.</param>
    /// <param name="accelerator">Keyboard shortcut (e.g., "Ctrl+S"), or null.</param>
    void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null);

    /// <summary>
    /// Insert a menu item after an existing item.
    /// </summary>
    /// <param name="menuLabel">Label of the parent menu.</param>
    /// <param name="afterItemId">ID of the item to insert after.</param>
    /// <param name="itemId">Unique identifier for the new item.</param>
    /// <param name="itemLabel">Display label for the item.</param>
    /// <param name="accelerator">Keyboard shortcut, or null.</param>
    void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null);

    /// <summary>
    /// Remove a menu item.
    /// </summary>
    /// <param name="menuLabel">Label of the parent menu.</param>
    /// <param name="itemId">ID of the item to remove.</param>
    void RemoveItem(string menuLabel, string itemId);

    /// <summary>
    /// Add a separator to a menu.
    /// </summary>
    /// <param name="menuLabel">Label of the parent menu.</param>
    void AddSeparator(string menuLabel);

    #endregion

    #region Item State

    /// <summary>
    /// Enable or disable a menu item.
    /// </summary>
    void SetItemEnabled(string menuLabel, string itemId, bool enabled);

    /// <summary>
    /// Set the checked state of a menu item.
    /// </summary>
    void SetItemChecked(string menuLabel, string itemId, bool isChecked);

    /// <summary>
    /// Update the label of a menu item.
    /// </summary>
    void SetItemLabel(string menuLabel, string itemId, string label);

    /// <summary>
    /// Update the keyboard accelerator of a menu item.
    /// </summary>
    void SetItemAccelerator(string menuLabel, string itemId, string accelerator);

    #endregion

    #region Events

    /// <summary>
    /// Raised when a menu item is clicked. The parameter is the item ID.
    /// </summary>
    event Action<string>? MenuItemClicked;

    #endregion
}
