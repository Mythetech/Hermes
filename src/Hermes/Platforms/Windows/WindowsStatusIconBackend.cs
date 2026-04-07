// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
#if WINDOWS
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsStatusIconBackend : IStatusIconBackend
{
    private const string WindowClassName = "HermesTrayIconWindow";
    private const uint WM_TRAYICON = PInvoke.WM_APP;

    private static readonly WNDPROC s_wndProc = TrayWindowProc;
    private static bool s_classRegistered;
    private static readonly object s_registrationLock = new();
    private static HINSTANCE s_hInstance;

    private static readonly Dictionary<HWND, WindowsStatusIconBackend> s_hwndToInstance = new();

    private HWND _hwnd;
    private HMENU _hMenu;
    private bool _iconAdded;
    private bool _disposed;
    private uint _nextMenuId = 20000;
    private string? _tempIconPath;

    private readonly Dictionary<uint, string> _menuIdToItemId = new();
    private readonly Dictionary<string, uint> _itemIdToMenuId = new();
    private readonly Dictionary<string, HMENU> _submenus = new();

    public event Action<string>? MenuItemClicked;
    public event Action? Clicked;
    public event Action? DoubleClicked;

    public void Initialize()
    {
        EnsureWindowClassRegistered();

        unsafe
        {
            fixed (char* className = WindowClassName)
            {
                _hwnd = PInvoke.CreateWindowEx(
                    WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
                    className,
                    className,
                    0,
                    0, 0, 0, 0,
                    HWND.Null,
                    HMENU.Null,
                    s_hInstance,
                    null);
            }
        }

        if (_hwnd.IsNull)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create tray message window");

        s_hwndToInstance[_hwnd] = this;

        _hMenu = PInvoke.CreatePopupMenu();
        if (_hMenu.IsNull)
            throw new InvalidOperationException("Failed to create tray popup menu");
    }

    public void Show()
    {
        if (_iconAdded) return;

        var nid = CreateNotifyIconData();
        nid.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;

        // Use default application icon
        nid.hIcon = GetDefaultIcon();

        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in nid))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to add tray icon");

        _iconAdded = true;
    }

    public void Hide()
    {
        if (!_iconAdded) return;

        var nid = CreateNotifyIconData();
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in nid);
        _iconAdded = false;
    }

    public void SetIcon(string filePath)
    {
        if (!File.Exists(filePath)) return;

        unsafe
        {
            fixed (char* path = filePath)
            {
                var hIcon = PInvoke.LoadImage(
                    HINSTANCE.Null,
                    path,
                    GDI_IMAGE_TYPE.IMAGE_ICON,
                    0, 0,
                    IMAGE_FLAGS.LR_LOADFROMFILE | IMAGE_FLAGS.LR_DEFAULTSIZE);

                if (!hIcon.IsNull)
                {
                    UpdateIcon(new HICON(hIcon.Value));
                }
            }
        }
    }

    public void SetIconFromStream(Stream stream)
    {
        // Write stream to a temp file, then load as icon
        CleanupTempIcon();

        _tempIconPath = Path.Combine(Path.GetTempPath(), $"hermes_tray_{Guid.NewGuid():N}.ico");
        using (var fs = File.Create(_tempIconPath))
        {
            stream.CopyTo(fs);
        }

        SetIcon(_tempIconPath);
    }

    public void SetTooltip(string tooltip)
    {
        if (!_iconAdded) return;

        var nid = CreateNotifyIconData();
        nid.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_TIP;

        // szTip is a 128-char buffer
        var tipSpan = nid.szTip.AsSpan();
        var truncated = tooltip.Length > 127 ? tooltip.AsSpan(0, 127) : tooltip.AsSpan();
        truncated.CopyTo(tipSpan);
        tipSpan[truncated.Length] = '\0';

        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in nid);
    }

    #region Menu Item Operations

    public void AddMenuItem(string itemId, string label)
    {
        var id = _nextMenuId++;
        _menuIdToItemId[id] = itemId;
        _itemIdToMenuId[itemId] = id;

        PInvoke.AppendMenu(_hMenu, MENU_ITEM_FLAGS.MF_STRING, id, label);
    }

    public void AddMenuSeparator()
    {
        PInvoke.AppendMenu(_hMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (string?)null);
    }

    public void RemoveMenuItem(string itemId)
    {
        if (!_itemIdToMenuId.TryGetValue(itemId, out var id))
            return;

        PInvoke.RemoveMenu(_hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND);
        _itemIdToMenuId.Remove(itemId);
        _menuIdToItemId.Remove(id);
    }

    public void ClearMenu()
    {
        while (PInvoke.GetMenuItemCount(_hMenu) > 0)
        {
            PInvoke.RemoveMenu(_hMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION);
        }

        _menuIdToItemId.Clear();
        _itemIdToMenuId.Clear();

        // Destroy submenus
        foreach (var submenu in _submenus.Values)
        {
            PInvoke.DestroyMenu(submenu);
        }
        _submenus.Clear();
    }

    #endregion

    #region Menu Item State

    public void SetMenuItemEnabled(string itemId, bool enabled)
    {
        if (!_itemIdToMenuId.TryGetValue(itemId, out var id))
            return;

        var flags = enabled ? MENU_ITEM_FLAGS.MF_ENABLED : MENU_ITEM_FLAGS.MF_GRAYED;
        PInvoke.EnableMenuItem(_hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | flags);
    }

    public void SetMenuItemChecked(string itemId, bool isChecked)
    {
        if (!_itemIdToMenuId.TryGetValue(itemId, out var id))
            return;

        var flags = isChecked ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED;
        PInvoke.CheckMenuItem(_hMenu, id, (uint)(MENU_ITEM_FLAGS.MF_BYCOMMAND | flags));
    }

    public void SetMenuItemLabel(string itemId, string label)
    {
        if (!_itemIdToMenuId.TryGetValue(itemId, out var id))
            return;

        PInvoke.ModifyMenu(_hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | MENU_ITEM_FLAGS.MF_STRING,
            id, label);
    }

    #endregion

    #region Submenu Operations

    public void AddSubmenu(string submenuId, string label)
    {
        var hSubmenu = PInvoke.CreatePopupMenu();
        if (hSubmenu.IsNull)
            throw new InvalidOperationException($"Failed to create submenu '{submenuId}'");

        _submenus[submenuId] = hSubmenu;

        unsafe
        {
            PInvoke.AppendMenu(_hMenu, MENU_ITEM_FLAGS.MF_POPUP, (nuint)hSubmenu.Value, label);
        }
    }

    public void AddSubmenuItem(string submenuId, string itemId, string label)
    {
        if (!_submenus.TryGetValue(submenuId, out var hSubmenu))
            throw new ArgumentException($"Submenu '{submenuId}' not found", nameof(submenuId));

        var id = _nextMenuId++;
        _menuIdToItemId[id] = itemId;
        _itemIdToMenuId[itemId] = id;

        PInvoke.AppendMenu(hSubmenu, MENU_ITEM_FLAGS.MF_STRING, id, label);
    }

    public void AddSubmenuSeparator(string submenuId)
    {
        if (!_submenus.TryGetValue(submenuId, out var hSubmenu))
            throw new ArgumentException($"Submenu '{submenuId}' not found", nameof(submenuId));

        PInvoke.AppendMenu(hSubmenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (string?)null);
    }

    public void ClearSubmenu(string submenuId)
    {
        if (!_submenus.TryGetValue(submenuId, out var hSubmenu))
            return;

        while (PInvoke.GetMenuItemCount(hSubmenu) > 0)
        {
            PInvoke.RemoveMenu(hSubmenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION);
        }
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_iconAdded)
        {
            var nid = CreateNotifyIconData();
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in nid);
            _iconAdded = false;
        }

        if (!_hMenu.IsNull)
        {
            PInvoke.DestroyMenu(_hMenu);
            _hMenu = HMENU.Null;
        }

        foreach (var submenu in _submenus.Values)
        {
            PInvoke.DestroyMenu(submenu);
        }
        _submenus.Clear();

        if (!_hwnd.IsNull)
        {
            s_hwndToInstance.Remove(_hwnd);
            PInvoke.DestroyWindow(_hwnd);
            _hwnd = HWND.Null;
        }

        CleanupTempIcon();
    }

    #region Private Helpers

    private static void EnsureWindowClassRegistered()
    {
        if (s_classRegistered) return;

        lock (s_registrationLock)
        {
            if (s_classRegistered) return;

            s_hInstance = PInvoke.GetModuleHandle((PCWSTR)null);

            unsafe
            {
                fixed (char* className = WindowClassName)
                {
                    var wcx = new WNDCLASSEXW
                    {
                        cbSize = (uint)sizeof(WNDCLASSEXW),
                        lpfnWndProc = s_wndProc,
                        hInstance = s_hInstance,
                        lpszClassName = className
                    };

                    var atom = PInvoke.RegisterClassEx(in wcx);
                    if (atom == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register tray window class");
                }
            }

            s_classRegistered = true;
        }
    }

    private static LRESULT TrayWindowProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        if (!s_hwndToInstance.TryGetValue(hwnd, out var instance))
            return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);

        switch (uMsg)
        {
            case WM_TRAYICON:
                return instance.HandleTrayMessage(lParam);

            case PInvoke.WM_COMMAND:
                return instance.HandleCommand(wParam);

            default:
                return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);
        }
    }

    private LRESULT HandleTrayMessage(LPARAM lParam)
    {
        uint mouseMsg = (uint)(lParam.Value & 0xFFFF);

        switch (mouseMsg)
        {
            case PInvoke.WM_LBUTTONUP:
                Clicked?.Invoke();
                break;

            case PInvoke.WM_LBUTTONDBLCLK:
                DoubleClicked?.Invoke();
                break;

            case PInvoke.WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }

        return new LRESULT(0);
    }

    private LRESULT HandleCommand(WPARAM wParam)
    {
        uint menuId = (uint)(wParam.Value & 0xFFFF);
        if (_menuIdToItemId.TryGetValue(menuId, out var itemId))
        {
            MenuItemClicked?.Invoke(itemId);
        }

        return new LRESULT(0);
    }

    private void ShowContextMenu()
    {
        if (PInvoke.GetMenuItemCount(_hMenu) == 0) return;

        // Required to make the menu dismiss properly when clicking outside
        PInvoke.SetForegroundWindow(_hwnd);

        PInvoke.GetCursorPos(out var pt);

        unsafe
        {
            PInvoke.TrackPopupMenu(
                _hMenu,
                TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN,
                pt.X, pt.Y,
                0,
                _hwnd,
                null);
        }

        // Send an empty message to force the menu to close (Win32 workaround)
        PInvoke.PostMessage(_hwnd, PInvoke.WM_USER, 0, 0);
    }

    private NOTIFYICONDATAW CreateNotifyIconData()
    {
        var nid = new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1
        };
        return nid;
    }

    private void UpdateIcon(HICON hIcon)
    {
        if (!_iconAdded) return;

        var nid = CreateNotifyIconData();
        nid.uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
        nid.hIcon = hIcon;

        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in nid);
    }

    private static HICON GetDefaultIcon()
    {
        return PInvoke.LoadIcon(HINSTANCE.Null, PInvoke.IDI_APPLICATION);
    }

    private void CleanupTempIcon()
    {
        if (_tempIconPath is not null && File.Exists(_tempIconPath))
        {
            try { File.Delete(_tempIconPath); }
            catch { /* best effort */ }
            _tempIconPath = null;
        }
    }

    #endregion
}
#endif
