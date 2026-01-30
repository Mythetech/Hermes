namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific backend for dock menu operations.
/// The dock menu appears when right-clicking the application's dock icon (macOS)
/// or taskbar icon (Windows). Custom items appear above the default system entries.
/// </summary>
public interface IDockMenuBackend : IDisposable
{
    #region Menu Item Operations

    /// <summary>
    /// Add a menu item to the dock menu.
    /// </summary>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="label">Display label for the item.</param>
    void AddItem(string itemId, string label);

    /// <summary>
    /// Add a separator to the dock menu.
    /// </summary>
    void AddSeparator();

    /// <summary>
    /// Remove a menu item.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    void RemoveItem(string itemId);

    /// <summary>
    /// Remove all items from the dock menu.
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

    #region Submenu Operations

    /// <summary>
    /// Add a submenu to the dock menu.
    /// </summary>
    /// <param name="submenuId">Unique identifier for the submenu.</param>
    /// <param name="label">Display label for the submenu.</param>
    void AddSubmenu(string submenuId, string label);

    /// <summary>
    /// Add an item to a submenu.
    /// </summary>
    /// <param name="submenuId">ID of the parent submenu.</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="label">Display label for the item.</param>
    void AddSubmenuItem(string submenuId, string itemId, string label);

    /// <summary>
    /// Add a separator to a submenu.
    /// </summary>
    /// <param name="submenuId">ID of the submenu.</param>
    void AddSubmenuSeparator(string submenuId);

    /// <summary>
    /// Clear all items from a submenu.
    /// </summary>
    /// <param name="submenuId">ID of the submenu to clear.</param>
    void ClearSubmenu(string submenuId);

    #endregion

    #region Events

    /// <summary>
    /// Raised when a menu item is clicked. The parameter is the item ID.
    /// </summary>
    event Action<string>? MenuItemClicked;

    #endregion
}
