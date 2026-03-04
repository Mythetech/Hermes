// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
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
    /// <param name="accelerator">
    /// Keyboard shortcut in format "Modifier+Key" (e.g., "Ctrl+S", "Cmd+Shift+N"), or null.
    /// <para><b>Platform behavior:</b></para>
    /// <para><b>macOS:</b> Use "Cmd+" for Command key. Accelerators are enforced by the OS.</para>
    /// <para><b>Windows:</b> Use "Ctrl+". Requires accelerator table setup for enforcement.</para>
    /// <para><b>Linux:</b> Use "Ctrl+". Handled via GTK AccelGroup.</para>
    /// </param>
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
    /// <remarks>
    /// <para><b>Platform behavior:</b></para>
    /// <para><b>macOS/Windows:</b> Any item can be toggled. Works on regular menu items.</para>
    /// <para><b>Linux:</b> GTK requires items to be created as CheckMenuItem. Calling this on
    /// a regular MenuItem may not work. Consider creating checkable items as CheckMenuItem from the start.</para>
    /// </remarks>
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

    #region Submenu Operations

    /// <summary>
    /// Add a submenu to an existing menu.
    /// </summary>
    /// <param name="menuPath">Path to the parent menu (e.g., "File" or "File/New").</param>
    /// <param name="submenuLabel">Display label for the new submenu.</param>
    void AddSubmenu(string menuPath, string submenuLabel);

    /// <summary>
    /// Add a menu item to a submenu.
    /// </summary>
    /// <param name="menuPath">Path to the submenu (e.g., "File/New").</param>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="itemLabel">Display label for the item.</param>
    /// <param name="accelerator">Keyboard shortcut, or null.</param>
    void AddSubmenuItem(string menuPath, string itemId, string itemLabel, string? accelerator = null);

    /// <summary>
    /// Add a separator to a submenu.
    /// </summary>
    /// <param name="menuPath">Path to the submenu.</param>
    void AddSubmenuSeparator(string menuPath);

    #endregion

    #region App Menu Operations

    /// <summary>
    /// Gets the application name used for the app menu.
    /// </summary>
    /// <remarks>
    /// <para><b>Platform behavior:</b></para>
    /// <para><b>macOS:</b> Returns the app name shown in the system app menu (first menu).
    /// Items added here appear under the application name menu alongside standard system items
    /// like About, Preferences, Services, Hide, and Quit.</para>
    /// <para><b>Windows/Linux:</b> Creates a standard top-level menu with the app name.
    /// This is not a true system menu - it's a regular menu styled to look like one.
    /// You have full control over its contents.</para>
    /// </remarks>
    string AppName { get; }

    /// <summary>
    /// Add an item to the app menu.
    /// </summary>
    /// <param name="itemId">Unique identifier for the item.</param>
    /// <param name="itemLabel">Display label (e.g., "About MyApp", "Settings...").</param>
    /// <param name="accelerator">Keyboard shortcut, or null.</param>
    /// <param name="position">
    /// Position hint for macOS: "before-quit", "after-about", "top", or null for end.
    /// On Windows/Linux, items are always appended (position is ignored).
    /// </param>
    /// <remarks>
    /// <para><b>Platform behavior:</b></para>
    /// <para><b>macOS:</b> Adds to the system app menu. The position parameter controls placement
    /// relative to system items (About, Quit, etc.). Use "before-quit" for most app-specific items.</para>
    /// <para><b>Windows/Linux:</b> Creates or uses a top-level menu with the app name. The position
    /// parameter is ignored; items are appended. System items like Quit must be added manually.</para>
    /// </remarks>
    void AddAppMenuItem(string itemId, string itemLabel, string? accelerator = null, string? position = null);

    /// <summary>
    /// Add a separator to the app menu.
    /// </summary>
    /// <param name="position">Position hint, or null for end.</param>
    void AddAppMenuSeparator(string? position = null);

    /// <summary>
    /// Remove an item from the app menu.
    /// </summary>
    /// <param name="itemId">ID of the item to remove.</param>
    void RemoveAppMenuItem(string itemId);

    #endregion
}
