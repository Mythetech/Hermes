// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific backend for system tray icon operations.
/// </summary>
public interface IStatusIconBackend : IDisposable
{
    #region Lifecycle

    /// <summary>
    /// Initialize the native status icon resources.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Show the status icon in the system tray.
    /// </summary>
    void Show();

    /// <summary>
    /// Hide the status icon from the system tray without disposing.
    /// </summary>
    void Hide();

    #endregion

    #region Icon

    /// <summary>
    /// Set the icon from a file path (.png, .ico).
    /// </summary>
    void SetIcon(string filePath);

    /// <summary>
    /// Set the icon from a stream (embedded resource or memory).
    /// </summary>
    void SetIconFromStream(Stream stream);

    /// <summary>
    /// Set the tooltip text displayed on hover.
    /// </summary>
    void SetTooltip(string tooltip);

    #endregion

    #region Menu Item Operations

    /// <summary>
    /// Add a menu item to the tray context menu.
    /// </summary>
    void AddMenuItem(string itemId, string label);

    /// <summary>
    /// Add a separator to the tray context menu.
    /// </summary>
    void AddMenuSeparator();

    /// <summary>
    /// Remove a menu item by ID.
    /// </summary>
    void RemoveMenuItem(string itemId);

    /// <summary>
    /// Remove all items from the tray context menu.
    /// </summary>
    void ClearMenu();

    #endregion

    #region Menu Item State

    /// <summary>
    /// Enable or disable a menu item.
    /// </summary>
    void SetMenuItemEnabled(string itemId, bool enabled);

    /// <summary>
    /// Set the checked state of a menu item.
    /// </summary>
    void SetMenuItemChecked(string itemId, bool isChecked);

    /// <summary>
    /// Update the label of a menu item.
    /// </summary>
    void SetMenuItemLabel(string itemId, string label);

    #endregion

    #region Submenu Operations

    /// <summary>
    /// Add a submenu to the tray context menu.
    /// </summary>
    void AddSubmenu(string submenuId, string label);

    /// <summary>
    /// Add an item to a submenu.
    /// </summary>
    void AddSubmenuItem(string submenuId, string itemId, string label);

    /// <summary>
    /// Add a separator to a submenu.
    /// </summary>
    void AddSubmenuSeparator(string submenuId);

    /// <summary>
    /// Clear all items from a submenu.
    /// </summary>
    void ClearSubmenu(string submenuId);

    #endregion

    #region Position

    /// <summary>
    /// Get the screen position and size of the status icon.
    /// Returns (x, y, width, height) in screen coordinates with top-left origin.
    /// Returns (0, 0, 0, 0) if the position cannot be determined.
    /// </summary>
    (int X, int Y, int Width, int Height) GetScreenPosition();

    #endregion

    #region Events

    /// <summary>
    /// Raised when a menu item is clicked. The parameter is the item ID.
    /// </summary>
    event Action<string>? MenuItemClicked;

    /// <summary>
    /// Raised when the tray icon is left-clicked.
    /// Windows and macOS only; not fired on Linux (AppIndicator opens the menu instead).
    /// </summary>
    event Action? Clicked;

    /// <summary>
    /// Raised when the tray icon is double-clicked.
    /// Windows only; not fired on macOS or Linux.
    /// </summary>
    event Action? DoubleClicked;

    #endregion
}
