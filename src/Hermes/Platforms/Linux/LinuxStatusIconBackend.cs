// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.Linux;

/// <summary>
/// Linux implementation of IStatusIconBackend using libappindicator3.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxStatusIconBackend : IStatusIconBackend
{
    private IntPtr _handle;
    private readonly LinuxNativeDelegates.MenuItemCallback _menuCallback;
    private bool _disposed;

    public event Action<string>? MenuItemClicked;
    // Clicked and DoubleClicked are not supported on Linux (AppIndicator opens the menu instead)
#pragma warning disable CS0067
    public event Action? Clicked;
    public event Action? DoubleClicked;
#pragma warning restore CS0067

    internal LinuxStatusIconBackend()
    {
        // Create callback delegate and keep it alive
        _menuCallback = new LinuxNativeDelegates.MenuItemCallback(OnNativeMenuItemClicked);

        // Create native status icon (Linux has no click callback since AppIndicator doesn't support raw clicks)
        _handle = LinuxNative.StatusIconCreate(
            Marshal.GetFunctionPointerForDelegate(_menuCallback));
    }

    #region Lifecycle

    public void Initialize()
    {
        EnsureNotDisposed();
        // AppIndicator is created in the constructor; nothing additional needed
    }

    public void Show()
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconShow(_handle);
    }

    public void Hide()
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconHide(_handle);
    }

    #endregion

    #region Icon

    public void SetIcon(string filePath)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconSetIconFromPath(_handle, filePath);
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
                LinuxNative.StatusIconSetIconFromData(_handle, (IntPtr)ptr, data.Length);
            }
        }
    }

    public void SetTooltip(string tooltip)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconSetTooltip(_handle, tooltip);
    }

    #endregion

    #region Menu Item Operations

    public void AddMenuItem(string itemId, string label)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconAddItem(_handle, itemId, label);
    }

    public void AddMenuSeparator()
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconAddSeparator(_handle);
    }

    public void RemoveMenuItem(string itemId)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconRemoveItem(_handle, itemId);
    }

    public void ClearMenu()
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconClear(_handle);
    }

    #endregion

    #region Menu Item State

    public void SetMenuItemEnabled(string itemId, bool enabled)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconSetItemEnabled(_handle, itemId, enabled);
    }

    public void SetMenuItemChecked(string itemId, bool isChecked)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconSetItemChecked(_handle, itemId, isChecked);
    }

    public void SetMenuItemLabel(string itemId, string label)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconSetItemLabel(_handle, itemId, label);
    }

    #endregion

    #region Submenu Operations

    public void AddSubmenu(string submenuId, string label)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconAddSubmenu(_handle, submenuId, label);
    }

    public void AddSubmenuItem(string submenuId, string itemId, string label)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconAddSubmenuItem(_handle, submenuId, itemId, label);
    }

    public void AddSubmenuSeparator(string submenuId)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconAddSubmenuSeparator(_handle, submenuId);
    }

    public void ClearSubmenu(string submenuId)
    {
        EnsureNotDisposed();
        LinuxNative.StatusIconClearSubmenu(_handle, submenuId);
    }

    #endregion

    #region Position

    public (int X, int Y, int Width, int Height) GetScreenPosition()
    {
        // Linux AppIndicator does not expose the tray icon position
        return (0, 0, 0, 0);
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
            throw new ObjectDisposedException(nameof(LinuxStatusIconBackend));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            LinuxNative.StatusIconDestroy(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~LinuxStatusIconBackend()
    {
        Dispose();
    }

    #endregion
}
