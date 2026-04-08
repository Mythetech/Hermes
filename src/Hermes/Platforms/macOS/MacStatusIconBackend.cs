// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of IStatusIconBackend using NSStatusItem.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacStatusIconBackend : IStatusIconBackend
{
    private IntPtr _handle;
    private readonly MacNativeDelegates.MenuItemCallback _menuCallback;
    private readonly MacNativeDelegates.InvokeCallback _clickCallback;
    private bool _disposed;

    public event Action<string>? MenuItemClicked;
    public event Action? Clicked;
    // DoubleClicked is not supported on macOS (no native double-click on NSStatusItem)
#pragma warning disable CS0067
    public event Action? DoubleClicked;
#pragma warning restore CS0067

    internal MacStatusIconBackend()
    {
        // Create callback delegates and keep them alive
        _menuCallback = new MacNativeDelegates.MenuItemCallback(OnNativeMenuItemClicked);
        _clickCallback = new MacNativeDelegates.InvokeCallback(OnNativeClicked);

        // Create native status icon
        _handle = MacNative.StatusIconCreate(
            Marshal.GetFunctionPointerForDelegate(_menuCallback),
            Marshal.GetFunctionPointerForDelegate(_clickCallback));
    }

    #region Lifecycle

    public void Initialize()
    {
        EnsureNotDisposed();
        // NSStatusItem is created in the constructor; nothing additional needed
    }

    public void Show()
    {
        EnsureNotDisposed();
        MacNative.StatusIconShow(_handle);
    }

    public void Hide()
    {
        EnsureNotDisposed();
        MacNative.StatusIconHide(_handle);
    }

    #endregion

    #region Icon

    public void SetIcon(string filePath)
    {
        EnsureNotDisposed();
        MacNative.StatusIconSetIconFromPath(_handle, filePath);
    }

    public void SetIconFromStream(Stream stream)
    {
        EnsureNotDisposed();

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();

        unsafe
        {
            fixed (byte* ptr = data)
            {
                MacNative.StatusIconSetIconFromData(_handle, (IntPtr)ptr, data.Length);
            }
        }
    }

    public void SetTooltip(string tooltip)
    {
        EnsureNotDisposed();
        MacNative.StatusIconSetTooltip(_handle, tooltip);
    }

    #endregion

    #region Menu Item Operations

    public void AddMenuItem(string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.StatusIconAddItem(_handle, itemId, label);
    }

    public void AddMenuSeparator()
    {
        EnsureNotDisposed();
        MacNative.StatusIconAddSeparator(_handle);
    }

    public void RemoveMenuItem(string itemId)
    {
        EnsureNotDisposed();
        MacNative.StatusIconRemoveItem(_handle, itemId);
    }

    public void ClearMenu()
    {
        EnsureNotDisposed();
        MacNative.StatusIconClear(_handle);
    }

    #endregion

    #region Menu Item State

    public void SetMenuItemEnabled(string itemId, bool enabled)
    {
        EnsureNotDisposed();
        MacNative.StatusIconSetItemEnabled(_handle, itemId, enabled);
    }

    public void SetMenuItemChecked(string itemId, bool isChecked)
    {
        EnsureNotDisposed();
        MacNative.StatusIconSetItemChecked(_handle, itemId, isChecked);
    }

    public void SetMenuItemLabel(string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.StatusIconSetItemLabel(_handle, itemId, label);
    }

    #endregion

    #region Submenu Operations

    public void AddSubmenu(string submenuId, string label)
    {
        EnsureNotDisposed();
        MacNative.StatusIconAddSubmenu(_handle, submenuId, label);
    }

    public void AddSubmenuItem(string submenuId, string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.StatusIconAddSubmenuItem(_handle, submenuId, itemId, label);
    }

    public void AddSubmenuSeparator(string submenuId)
    {
        EnsureNotDisposed();
        MacNative.StatusIconAddSubmenuSeparator(_handle, submenuId);
    }

    public void ClearSubmenu(string submenuId)
    {
        EnsureNotDisposed();
        MacNative.StatusIconClearSubmenu(_handle, submenuId);
    }

    #endregion

    #region Position

    public (int X, int Y, int Width, int Height) GetScreenPosition()
    {
        EnsureNotDisposed();
        MacNative.StatusIconGetScreenPosition(_handle, out int x, out int y, out int width, out int height);
        return (x, y, width, height);
    }

    #endregion

    #region Private Helpers

    private void OnNativeMenuItemClicked(IntPtr itemIdPtr)
    {
        var itemId = Marshal.PtrToStringUTF8(itemIdPtr) ?? "";
        MenuItemClicked?.Invoke(itemId);
    }

    private void OnNativeClicked()
    {
        Clicked?.Invoke();
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MacStatusIconBackend));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            MacNative.StatusIconDestroy(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~MacStatusIconBackend()
    {
        Dispose();
    }

    #endregion
}
