// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of IContextMenuBackend using native NSMenu popup.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacContextMenuBackend : IContextMenuBackend
{
    private readonly IntPtr _windowHandle;
    private IntPtr _contextMenuHandle;
    private readonly MacNativeDelegates.MenuItemCallback _menuCallback;
    private bool _disposed;

    public event Action<string>? MenuItemClicked;

    internal MacContextMenuBackend(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;

        // Create callback delegate and keep it alive
        _menuCallback = new MacNativeDelegates.MenuItemCallback(OnNativeMenuItemClicked);

        // Create native context menu
        _contextMenuHandle = MacNative.ContextMenuCreate(_windowHandle, Marshal.GetFunctionPointerForDelegate(_menuCallback));
    }

    #region Menu Item Operations

    public void AddItem(string itemId, string label, string? accelerator = null)
    {
        EnsureNotDisposed();
        MacNative.ContextMenuAddItem(_contextMenuHandle, itemId, label, accelerator);
    }

    public void AddSeparator()
    {
        EnsureNotDisposed();
        MacNative.ContextMenuAddSeparator(_contextMenuHandle);
    }

    public void RemoveItem(string itemId)
    {
        EnsureNotDisposed();
        MacNative.ContextMenuRemoveItem(_contextMenuHandle, itemId);
    }

    public void Clear()
    {
        EnsureNotDisposed();
        MacNative.ContextMenuClear(_contextMenuHandle);
    }

    #endregion

    #region Item State

    public void SetItemEnabled(string itemId, bool enabled)
    {
        EnsureNotDisposed();
        MacNative.ContextMenuSetItemEnabled(_contextMenuHandle, itemId, enabled);
    }

    public void SetItemChecked(string itemId, bool isChecked)
    {
        EnsureNotDisposed();
        MacNative.ContextMenuSetItemChecked(_contextMenuHandle, itemId, isChecked);
    }

    public void SetItemLabel(string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.ContextMenuSetItemLabel(_contextMenuHandle, itemId, label);
    }

    #endregion

    #region Display

    public void Show(int x, int y)
    {
        EnsureNotDisposed();
        MacNative.ContextMenuShow(_contextMenuHandle, x, y);
    }

    public void Hide()
    {
        EnsureNotDisposed();
        MacNative.ContextMenuHide(_contextMenuHandle);
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
            throw new ObjectDisposedException(nameof(MacContextMenuBackend));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_contextMenuHandle != IntPtr.Zero)
        {
            MacNative.ContextMenuDestroy(_contextMenuHandle);
            _contextMenuHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~MacContextMenuBackend()
    {
        Dispose();
    }

    #endregion
}
