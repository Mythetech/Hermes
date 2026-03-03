// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of IDockMenuBackend using the native dock menu API.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacDockMenuBackend : IDockMenuBackend
{
    private IntPtr _dockMenuHandle;
    private readonly MacNativeDelegates.MenuItemCallback _menuCallback;
    private bool _disposed;

    public event Action<string>? MenuItemClicked;

    internal MacDockMenuBackend()
    {
        // Create callback delegate and keep it alive
        _menuCallback = new MacNativeDelegates.MenuItemCallback(OnNativeMenuItemClicked);

        // Create native dock menu
        _dockMenuHandle = MacNative.DockMenuCreate(Marshal.GetFunctionPointerForDelegate(_menuCallback));
    }

    #region Menu Item Operations

    public void AddItem(string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.DockMenuAddItem(_dockMenuHandle, itemId, label);
    }

    public void AddSeparator()
    {
        EnsureNotDisposed();
        MacNative.DockMenuAddSeparator(_dockMenuHandle);
    }

    public void RemoveItem(string itemId)
    {
        EnsureNotDisposed();
        MacNative.DockMenuRemoveItem(_dockMenuHandle, itemId);
    }

    public void Clear()
    {
        EnsureNotDisposed();
        MacNative.DockMenuClear(_dockMenuHandle);
    }

    #endregion

    #region Item State

    public void SetItemEnabled(string itemId, bool enabled)
    {
        EnsureNotDisposed();
        MacNative.DockMenuSetItemEnabled(_dockMenuHandle, itemId, enabled);
    }

    public void SetItemChecked(string itemId, bool isChecked)
    {
        EnsureNotDisposed();
        MacNative.DockMenuSetItemChecked(_dockMenuHandle, itemId, isChecked);
    }

    public void SetItemLabel(string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.DockMenuSetItemLabel(_dockMenuHandle, itemId, label);
    }

    #endregion

    #region Submenu Operations

    public void AddSubmenu(string submenuId, string label)
    {
        EnsureNotDisposed();
        MacNative.DockMenuAddSubmenu(_dockMenuHandle, submenuId, label);
    }

    public void AddSubmenuItem(string submenuId, string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.DockMenuAddSubmenuItem(_dockMenuHandle, submenuId, itemId, label);
    }

    public void AddSubmenuSeparator(string submenuId)
    {
        EnsureNotDisposed();
        MacNative.DockMenuAddSubmenuSeparator(_dockMenuHandle, submenuId);
    }

    public void ClearSubmenu(string submenuId)
    {
        EnsureNotDisposed();
        MacNative.DockMenuClearSubmenu(_dockMenuHandle, submenuId);
    }

    #endregion

    #region Private Helpers

    private void OnNativeMenuItemClicked(IntPtr itemIdPtr)
    {
        var itemId = Marshal.PtrToStringUTF8(itemIdPtr) ?? "";
        MenuItemClicked?.Invoke(itemId);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MacDockMenuBackend));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_dockMenuHandle != IntPtr.Zero)
        {
            MacNative.DockMenuDestroy(_dockMenuHandle);
            _dockMenuHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~MacDockMenuBackend()
    {
        Dispose();
    }

    #endregion
}
