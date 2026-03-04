// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxContextMenuBackend : IContextMenuBackend
{
    private readonly IntPtr _windowHandle;
    private readonly IntPtr _contextMenuHandle;
    private readonly LinuxNativeDelegates.MenuItemCallback _menuCallback;
    private bool _disposed;

    public event Action<string>? MenuItemClicked;

    internal LinuxContextMenuBackend(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;

        // Create callback and keep it alive for the menu's lifetime
        _menuCallback = OnMenuItemClicked;

        _contextMenuHandle = LinuxNative.ContextMenuCreate(
            windowHandle,
            Marshal.GetFunctionPointerForDelegate(_menuCallback));

        if (_contextMenuHandle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create native context menu.");
    }

    public void AddItem(string itemId, string label, string? accelerator = null)
    {
        LinuxNative.ContextMenuAddItem(_contextMenuHandle, itemId, label, accelerator);
    }

    public void AddSeparator()
    {
        LinuxNative.ContextMenuAddSeparator(_contextMenuHandle);
    }

    public void RemoveItem(string itemId)
    {
        LinuxNative.ContextMenuRemoveItem(_contextMenuHandle, itemId);
    }

    public void Clear()
    {
        LinuxNative.ContextMenuClear(_contextMenuHandle);
    }

    public void SetItemEnabled(string itemId, bool enabled)
    {
        LinuxNative.ContextMenuSetItemEnabled(_contextMenuHandle, itemId, enabled);
    }

    public void SetItemChecked(string itemId, bool isChecked)
    {
        LinuxNative.ContextMenuSetItemChecked(_contextMenuHandle, itemId, isChecked);
    }

    public void SetItemLabel(string itemId, string label)
    {
        LinuxNative.ContextMenuSetItemLabel(_contextMenuHandle, itemId, label);
    }

    public void Show(int x, int y)
    {
        LinuxNative.ContextMenuShow(_contextMenuHandle, x, y);
    }

    public void Hide()
    {
        LinuxNative.ContextMenuHide(_contextMenuHandle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_contextMenuHandle != IntPtr.Zero)
        {
            LinuxNative.ContextMenuDestroy(_contextMenuHandle);
        }
    }

    private void OnMenuItemClicked(IntPtr itemIdPtr)
    {
        var itemId = Marshal.PtrToStringUTF8(itemIdPtr) ?? "";
        MenuItemClicked?.Invoke(itemId);
    }
}
