// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxMenuBackend : IMenuBackend
{
    private readonly IntPtr _windowHandle;
    private readonly IntPtr _menuHandle;
    private readonly LinuxNativeDelegates.MenuItemCallback _menuCallback;
    private string? _appName;

    public event Action<string>? MenuItemClicked;

    internal LinuxMenuBackend(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;

        // Create callback and keep it alive for the menu's lifetime
        _menuCallback = OnMenuItemClicked;

        _menuHandle = LinuxNative.MenuCreate(
            windowHandle,
            Marshal.GetFunctionPointerForDelegate(_menuCallback));

        if (_menuHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create native menu.");
    }

    public void AddMenu(string label, int insertIndex = -1)
    {
        LinuxNative.MenuAddMenu(_menuHandle, label, insertIndex);
    }

    public void RemoveMenu(string label)
    {
        LinuxNative.MenuRemoveMenu(_menuHandle, label);
    }

    public void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null)
    {
        LinuxNative.MenuAddItem(_menuHandle, menuLabel, itemId, itemLabel, accelerator);
    }

    public void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null)
    {
        LinuxNative.MenuInsertItem(_menuHandle, menuLabel, afterItemId, itemId, itemLabel, accelerator);
    }

    public void RemoveItem(string menuLabel, string itemId)
    {
        LinuxNative.MenuRemoveItem(_menuHandle, menuLabel, itemId);
    }

    public void AddSeparator(string menuLabel)
    {
        LinuxNative.MenuAddSeparator(_menuHandle, menuLabel);
    }

    public void SetItemEnabled(string menuLabel, string itemId, bool enabled)
    {
        LinuxNative.MenuSetItemEnabled(_menuHandle, menuLabel, itemId, enabled);
    }

    public void SetItemChecked(string menuLabel, string itemId, bool isChecked)
    {
        LinuxNative.MenuSetItemChecked(_menuHandle, menuLabel, itemId, isChecked);
    }

    public void SetItemLabel(string menuLabel, string itemId, string label)
    {
        LinuxNative.MenuSetItemLabel(_menuHandle, menuLabel, itemId, label);
    }

    public void SetItemAccelerator(string menuLabel, string itemId, string accelerator)
    {
        LinuxNative.MenuSetItemAccelerator(_menuHandle, menuLabel, itemId, accelerator);
    }

    #region Submenu Operations

    public void AddSubmenu(string menuPath, string submenuLabel)
    {
        LinuxNative.MenuAddSubmenu(_menuHandle, menuPath, submenuLabel);
    }

    public void AddSubmenuItem(string menuPath, string itemId, string itemLabel, string? accelerator = null)
    {
        LinuxNative.MenuAddSubmenuItem(_menuHandle, menuPath, itemId, itemLabel, accelerator);
    }

    public void AddSubmenuSeparator(string menuPath)
    {
        LinuxNative.MenuAddSubmenuSeparator(_menuHandle, menuPath);
    }

    #endregion

    #region App Menu Operations

    public string AppName => _appName ??= System.Diagnostics.Process.GetCurrentProcess().ProcessName;

    public void AddAppMenuItem(string itemId, string itemLabel, string? accelerator = null, string? position = null)
    {
        // On Linux, app menu is just another top-level menu with the app name
        // First ensure the app menu exists
        EnsureAppMenu();

        // Add item to the app menu
        LinuxNative.MenuAddItem(_menuHandle, AppName, itemId, itemLabel, accelerator);
    }

    public void AddAppMenuSeparator(string? position = null)
    {
        EnsureAppMenu();
        LinuxNative.MenuAddSeparator(_menuHandle, AppName);
    }

    public void RemoveAppMenuItem(string itemId)
    {
        LinuxNative.MenuRemoveItem(_menuHandle, AppName, itemId);
    }

    private bool _appMenuCreated;

    private void EnsureAppMenu()
    {
        if (_appMenuCreated) return;

        // Insert app menu at position 0 (leftmost)
        LinuxNative.MenuAddMenu(_menuHandle, AppName, 0);
        _appMenuCreated = true;
    }

    #endregion

    private void OnMenuItemClicked(IntPtr itemIdPtr)
    {
        var itemId = Marshal.PtrToStringUTF8(itemIdPtr) ?? "";
        MenuItemClicked?.Invoke(itemId);
    }
}
