using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of IMenuBackend using native NSMenu.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacMenuBackend : IMenuBackend
{
    private readonly IntPtr _windowHandle;
    private IntPtr _menuHandle;
    private readonly MacNativeDelegates.MenuItemCallback _menuCallback;
    private bool _disposed;

    public MacMenuBackend(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;

        // Create callback delegate and keep it alive
        _menuCallback = new MacNativeDelegates.MenuItemCallback(OnNativeMenuItemClicked);

        // Create native menu
        _menuHandle = MacNative.MenuCreate(_windowHandle, Marshal.GetFunctionPointerForDelegate(_menuCallback));
    }

    #region Menu Bar Operations

    public void AddMenu(string label, int insertIndex = -1)
    {
        EnsureNotDisposed();
        MacNative.MenuAddMenu(_menuHandle, label, insertIndex);
    }

    public void RemoveMenu(string label)
    {
        EnsureNotDisposed();
        MacNative.MenuRemoveMenu(_menuHandle, label);
    }

    #endregion

    #region Menu Item Operations

    public void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null)
    {
        EnsureNotDisposed();
        MacNative.MenuAddItem(_menuHandle, menuLabel, itemId, itemLabel, accelerator);
    }

    public void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null)
    {
        EnsureNotDisposed();
        MacNative.MenuInsertItem(_menuHandle, menuLabel, afterItemId, itemId, itemLabel, accelerator);
    }

    public void RemoveItem(string menuLabel, string itemId)
    {
        EnsureNotDisposed();
        MacNative.MenuRemoveItem(_menuHandle, menuLabel, itemId);
    }

    public void AddSeparator(string menuLabel)
    {
        EnsureNotDisposed();
        MacNative.MenuAddSeparator(_menuHandle, menuLabel);
    }

    #endregion

    #region Item State

    public void SetItemEnabled(string menuLabel, string itemId, bool enabled)
    {
        EnsureNotDisposed();
        MacNative.MenuSetItemEnabled(_menuHandle, menuLabel, itemId, enabled);
    }

    public void SetItemChecked(string menuLabel, string itemId, bool isChecked)
    {
        EnsureNotDisposed();
        MacNative.MenuSetItemChecked(_menuHandle, menuLabel, itemId, isChecked);
    }

    public void SetItemLabel(string menuLabel, string itemId, string label)
    {
        EnsureNotDisposed();
        MacNative.MenuSetItemLabel(_menuHandle, menuLabel, itemId, label);
    }

    public void SetItemAccelerator(string menuLabel, string itemId, string accelerator)
    {
        EnsureNotDisposed();
        MacNative.MenuSetItemAccelerator(_menuHandle, menuLabel, itemId, accelerator);
    }

    #endregion

    #region Events

    public event Action<string>? MenuItemClicked;

    private void OnNativeMenuItemClicked(IntPtr itemIdPtr)
    {
        var itemId = Marshal.PtrToStringUTF8(itemIdPtr) ?? "";
        MenuItemClicked?.Invoke(itemId);
    }

    #endregion

    #region Private Helpers

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MacMenuBackend));
    }

    #endregion

    #region IDisposable (implicit)

    ~MacMenuBackend()
    {
        Dispose();
    }

    private void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_menuHandle != IntPtr.Zero)
        {
            MacNative.MenuDestroy(_menuHandle);
            _menuHandle = IntPtr.Zero;
        }
    }

    #endregion
}
