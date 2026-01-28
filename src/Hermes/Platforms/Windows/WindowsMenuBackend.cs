using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsMenuBackend : IMenuBackend
{
    private readonly HWND _hwnd;
    private readonly HMENU _hMenuBar;

    private readonly Dictionary<string, HMENU> _menusByLabel = new();
    private readonly Dictionary<string, HMENU> _submenusByPath = new();
    private readonly Dictionary<string, uint> _itemIdByCommandId = new();
    private readonly Dictionary<uint, string> _commandIdByItemId = new();
    private uint _nextItemId = 1000;
    private HMENU? _appMenu;
    private string? _appName;

    // Accelerator table management
    private readonly List<ACCEL> _accelerators = new();
    private HACCEL _hAccelTable;
    private bool _accelTableDirty;

    public event Action<string>? MenuItemClicked;

    /// <summary>
    /// Gets the accelerator table handle for use with TranslateAccelerator.
    /// </summary>
    internal HACCEL AccelTable
    {
        get
        {
            if (_accelTableDirty)
                RebuildAccelTable();
            return _hAccelTable;
        }
    }

    internal WindowsMenuBackend(HWND hwnd)
    {
        _hwnd = hwnd;
        _hMenuBar = PInvoke.CreateMenu();

        if (_hMenuBar.IsNull)
            throw new InvalidOperationException("Failed to create menu bar");

        PInvoke.SetMenu(_hwnd, _hMenuBar);
    }

    public void AddMenu(string label, int insertIndex = -1)
    {
        var hPopup = PInvoke.CreatePopupMenu();

        if (hPopup.IsNull)
            throw new InvalidOperationException($"Failed to create popup menu for '{label}'");

        _menusByLabel[label] = hPopup;

        unsafe
        {
            if (insertIndex < 0)
            {
                PInvoke.AppendMenu(_hMenuBar, MENU_ITEM_FLAGS.MF_POPUP, (nuint)hPopup.Value, label);
            }
            else
            {
                PInvoke.InsertMenu(_hMenuBar, (uint)insertIndex,
                    MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_POPUP,
                    (nuint)hPopup.Value, label);
            }
        }

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void RemoveMenu(string label)
    {
        if (!_menusByLabel.TryGetValue(label, out var hMenu))
            return;

        int count = PInvoke.GetMenuItemCount(_hMenuBar);
        for (int i = 0; i < count; i++)
        {
            var hSubMenu = PInvoke.GetSubMenu(_hMenuBar, i);
            if (hSubMenu == hMenu)
            {
                PInvoke.RemoveMenu(_hMenuBar, (uint)i, MENU_ITEM_FLAGS.MF_BYPOSITION);
                break;
            }
        }

        _menusByLabel.Remove(label);
        PInvoke.DestroyMenu(hMenu);
        PInvoke.DrawMenuBar(_hwnd);
    }

    public void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            throw new ArgumentException($"Menu '{menuLabel}' not found", nameof(menuLabel));

        var id = _nextItemId++;
        _itemIdByCommandId[itemId] = id;
        _commandIdByItemId[id] = itemId;

        var displayLabel = FormatLabelWithAccelerator(itemLabel, accelerator);
        PInvoke.AppendMenu(hMenu, MENU_ITEM_FLAGS.MF_STRING, id, displayLabel);

        // Register accelerator for keyboard shortcut
        if (!string.IsNullOrEmpty(accelerator))
            RegisterAccelerator(id, accelerator);

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            throw new ArgumentException($"Menu '{menuLabel}' not found", nameof(menuLabel));

        if (!_itemIdByCommandId.TryGetValue(afterItemId, out var afterId))
            throw new ArgumentException($"Item '{afterItemId}' not found", nameof(afterItemId));

        var id = _nextItemId++;
        _itemIdByCommandId[itemId] = id;
        _commandIdByItemId[id] = itemId;

        int position = FindItemPosition(hMenu, afterId);

        var displayLabel = FormatLabelWithAccelerator(itemLabel, accelerator);
        PInvoke.InsertMenu(hMenu, (uint)(position + 1),
            MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING,
            id, displayLabel);

        // Register accelerator for keyboard shortcut
        if (!string.IsNullOrEmpty(accelerator))
            RegisterAccelerator(id, accelerator);

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void RemoveItem(string menuLabel, string itemId)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            return;

        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        PInvoke.RemoveMenu(hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND);
        _itemIdByCommandId.Remove(itemId);
        _commandIdByItemId.Remove(id);

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void AddSeparator(string menuLabel)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            throw new ArgumentException($"Menu '{menuLabel}' not found", nameof(menuLabel));

        PInvoke.AppendMenu(hMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (string?)null);
        PInvoke.DrawMenuBar(_hwnd);
    }

    public void SetItemEnabled(string menuLabel, string itemId, bool enabled)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            return;
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        var flags = enabled ? MENU_ITEM_FLAGS.MF_ENABLED : MENU_ITEM_FLAGS.MF_GRAYED;
        PInvoke.EnableMenuItem(hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | flags);
    }

    public void SetItemChecked(string menuLabel, string itemId, bool isChecked)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            return;
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        var flags = isChecked ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED;
        PInvoke.CheckMenuItem(hMenu, id, (uint)(MENU_ITEM_FLAGS.MF_BYCOMMAND | flags));
    }

    public void SetItemLabel(string menuLabel, string itemId, string label)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            return;
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        PInvoke.ModifyMenu(hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | MENU_ITEM_FLAGS.MF_STRING,
            id, label);
        PInvoke.DrawMenuBar(_hwnd);
    }

    public void SetItemAccelerator(string menuLabel, string itemId, string accelerator)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var hMenu))
            return;
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        var currentLabel = GetItemLabel(hMenu, id);
        if (currentLabel is null) return;

        var baseLabel = currentLabel.Split('\t')[0];
        var newLabel = FormatLabelWithAccelerator(baseLabel, accelerator);

        PInvoke.ModifyMenu(hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | MENU_ITEM_FLAGS.MF_STRING,
            id, newLabel);
        PInvoke.DrawMenuBar(_hwnd);
    }

    #region Submenu Operations

    public void AddSubmenu(string menuPath, string submenuLabel)
    {
        var parentMenu = FindMenuByPath(menuPath);
        if (parentMenu is null)
            throw new ArgumentException($"Menu '{menuPath}' not found", nameof(menuPath));

        var hSubmenu = PInvoke.CreatePopupMenu();
        if (hSubmenu.IsNull)
            throw new InvalidOperationException($"Failed to create submenu '{submenuLabel}'");

        unsafe
        {
            PInvoke.AppendMenu(parentMenu.Value, MENU_ITEM_FLAGS.MF_POPUP, (nuint)hSubmenu.Value, submenuLabel);
        }

        var fullPath = $"{menuPath}/{submenuLabel}";
        _submenusByPath[fullPath] = hSubmenu;

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void AddSubmenuItem(string menuPath, string itemId, string itemLabel, string? accelerator = null)
    {
        var menu = FindMenuByPath(menuPath);
        if (menu is null)
            throw new ArgumentException($"Menu '{menuPath}' not found", nameof(menuPath));

        var id = _nextItemId++;
        _itemIdByCommandId[itemId] = id;
        _commandIdByItemId[id] = itemId;

        var displayLabel = FormatLabelWithAccelerator(itemLabel, accelerator);
        PInvoke.AppendMenu(menu.Value, MENU_ITEM_FLAGS.MF_STRING, id, displayLabel);

        // Register accelerator for keyboard shortcut
        if (!string.IsNullOrEmpty(accelerator))
            RegisterAccelerator(id, accelerator);

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void AddSubmenuSeparator(string menuPath)
    {
        var menu = FindMenuByPath(menuPath);
        if (menu is null)
            throw new ArgumentException($"Menu '{menuPath}' not found", nameof(menuPath));

        PInvoke.AppendMenu(menu.Value, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (string?)null);
        PInvoke.DrawMenuBar(_hwnd);
    }

    #endregion

    #region App Menu Operations

    public string AppName => _appName ??= System.Diagnostics.Process.GetCurrentProcess().ProcessName;

    private HMENU EnsureAppMenu()
    {
        if (_appMenu.HasValue)
            return _appMenu.Value;

        var hPopup = PInvoke.CreatePopupMenu();
        if (hPopup.IsNull)
            throw new InvalidOperationException("Failed to create app menu");

        // Insert at position 0 (before File, Edit, etc.)
        unsafe
        {
            PInvoke.InsertMenu(_hMenuBar, 0,
                MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_POPUP,
                (nuint)hPopup.Value, AppName);
        }

        _appMenu = hPopup;
        PInvoke.DrawMenuBar(_hwnd);

        return hPopup;
    }

    public void AddAppMenuItem(string itemId, string itemLabel, string? accelerator = null, string? position = null)
    {
        var appMenu = EnsureAppMenu();

        var id = _nextItemId++;
        _itemIdByCommandId[itemId] = id;
        _commandIdByItemId[id] = itemId;

        var displayLabel = FormatLabelWithAccelerator(itemLabel, accelerator);

        // Windows doesn't have standard About/Quit positions, so just append
        PInvoke.AppendMenu(appMenu, MENU_ITEM_FLAGS.MF_STRING, id, displayLabel);

        // Register accelerator for keyboard shortcut
        if (!string.IsNullOrEmpty(accelerator))
            RegisterAccelerator(id, accelerator);

        PInvoke.DrawMenuBar(_hwnd);
    }

    public void AddAppMenuSeparator(string? position = null)
    {
        var appMenu = EnsureAppMenu();
        PInvoke.AppendMenu(appMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (string?)null);
        PInvoke.DrawMenuBar(_hwnd);
    }

    public void RemoveAppMenuItem(string itemId)
    {
        if (!_appMenu.HasValue) return;
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id)) return;

        PInvoke.RemoveMenu(_appMenu.Value, id, MENU_ITEM_FLAGS.MF_BYCOMMAND);
        _itemIdByCommandId.Remove(itemId);
        _commandIdByItemId.Remove(id);

        PInvoke.DrawMenuBar(_hwnd);
    }

    #endregion

    internal void HandleMenuCommand(uint menuId)
    {
        if (_commandIdByItemId.TryGetValue(menuId, out var commandId))
        {
            MenuItemClicked?.Invoke(commandId);
        }
    }

    private static string FormatLabelWithAccelerator(string label, string? accelerator)
    {
        if (string.IsNullOrEmpty(accelerator))
            return label;

        var displayAccel = accelerator
            .Replace("Cmd+", "Ctrl+", StringComparison.OrdinalIgnoreCase)
            .Replace("Command+", "Ctrl+", StringComparison.OrdinalIgnoreCase);

        return $"{label}\t{displayAccel}";
    }

    private static int FindItemPosition(HMENU hMenu, uint itemId)
    {
        int count = PInvoke.GetMenuItemCount(hMenu);
        for (int i = 0; i < count; i++)
        {
            var mii = new MENUITEMINFOW
            {
                cbSize = (uint)Marshal.SizeOf<MENUITEMINFOW>(),
                fMask = MENU_ITEM_MASK.MIIM_ID
            };

            unsafe
            {
                if (PInvoke.GetMenuItemInfo(hMenu, (uint)i, true, ref mii) && mii.wID == itemId)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private HMENU? FindMenuByPath(string path)
    {
        // Check submenu cache first
        if (_submenusByPath.TryGetValue(path, out var submenu))
            return submenu;

        // For single-component paths, it's a top-level menu
        if (!path.Contains('/'))
        {
            return _menusByLabel.TryGetValue(path, out var menu) ? menu : null;
        }

        return null;
    }

    private static string? GetItemLabel(HMENU hMenu, uint itemId)
    {
        var mii = new MENUITEMINFOW
        {
            cbSize = (uint)Marshal.SizeOf<MENUITEMINFOW>(),
            fMask = MENU_ITEM_MASK.MIIM_STRING
        };

        unsafe
        {
            if (!PInvoke.GetMenuItemInfo(hMenu, itemId, false, ref mii))
                return null;

            if (mii.cch == 0)
                return string.Empty;

            var buffer = new char[mii.cch + 1];
            fixed (char* ptr = buffer)
            {
                mii.dwTypeData = ptr;
                mii.cch++;

                if (!PInvoke.GetMenuItemInfo(hMenu, itemId, false, ref mii))
                    return null;
            }

            return new string(buffer, 0, (int)mii.cch);
        }
    }

    #region Accelerator Table Management

    private void RegisterAccelerator(uint menuId, string accelerator)
    {
        var accel = ParseAccelerator(accelerator, menuId);
        if (accel.HasValue)
        {
            _accelerators.Add(accel.Value);
            _accelTableDirty = true;
        }
    }

    private void RebuildAccelTable()
    {
        // Destroy existing table
        if (!_hAccelTable.IsNull)
        {
            PInvoke.DestroyAcceleratorTable(_hAccelTable);
            _hAccelTable = HACCEL.Null;
        }

        if (_accelerators.Count == 0)
        {
            _accelTableDirty = false;
            return;
        }

        // Create new table
        var accelArray = _accelerators.ToArray();
        unsafe
        {
            fixed (ACCEL* pAccel = accelArray)
            {
                _hAccelTable = PInvoke.CreateAcceleratorTable(pAccel, accelArray.Length);
            }
        }
        _accelTableDirty = false;
    }

    private static ACCEL? ParseAccelerator(string accelerator, uint menuId)
    {
        if (string.IsNullOrWhiteSpace(accelerator))
            return null;

        // Normalize: Cmd -> Ctrl, Command -> Ctrl
        var normalized = accelerator
            .Replace("Cmd+", "Ctrl+", StringComparison.OrdinalIgnoreCase)
            .Replace("Command+", "Ctrl+", StringComparison.OrdinalIgnoreCase);

        var modifiers = ACCEL_VIRT_FLAGS.FVIRTKEY;
        ushort key = 0;

        var parts = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var upper = trimmed.ToUpperInvariant();

            switch (upper)
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ACCEL_VIRT_FLAGS.FCONTROL;
                    break;
                case "ALT":
                    modifiers |= ACCEL_VIRT_FLAGS.FALT;
                    break;
                case "SHIFT":
                    modifiers |= ACCEL_VIRT_FLAGS.FSHIFT;
                    break;
                default:
                    // This should be the key
                    key = ParseKeyCode(upper);
                    break;
            }
        }

        if (key == 0)
            return null;

        return new ACCEL
        {
            fVirt = modifiers,
            key = key,
            cmd = (ushort)menuId
        };
    }

    private static ushort ParseKeyCode(string key)
    {
        // Single character (A-Z, 0-9)
        if (key.Length == 1)
        {
            var c = key[0];
            if (c is >= 'A' and <= 'Z')
                return (ushort)c; // VK_A through VK_Z match ASCII codes
            if (c is >= '0' and <= '9')
                return (ushort)c; // VK_0 through VK_9 match ASCII codes
        }

        // Function keys and special keys
        return key switch
        {
            "F1" => (ushort)VIRTUAL_KEY.VK_F1,
            "F2" => (ushort)VIRTUAL_KEY.VK_F2,
            "F3" => (ushort)VIRTUAL_KEY.VK_F3,
            "F4" => (ushort)VIRTUAL_KEY.VK_F4,
            "F5" => (ushort)VIRTUAL_KEY.VK_F5,
            "F6" => (ushort)VIRTUAL_KEY.VK_F6,
            "F7" => (ushort)VIRTUAL_KEY.VK_F7,
            "F8" => (ushort)VIRTUAL_KEY.VK_F8,
            "F9" => (ushort)VIRTUAL_KEY.VK_F9,
            "F10" => (ushort)VIRTUAL_KEY.VK_F10,
            "F11" => (ushort)VIRTUAL_KEY.VK_F11,
            "F12" => (ushort)VIRTUAL_KEY.VK_F12,
            "ENTER" or "RETURN" => (ushort)VIRTUAL_KEY.VK_RETURN,
            "ESCAPE" or "ESC" => (ushort)VIRTUAL_KEY.VK_ESCAPE,
            "TAB" => (ushort)VIRTUAL_KEY.VK_TAB,
            "SPACE" => (ushort)VIRTUAL_KEY.VK_SPACE,
            "BACKSPACE" or "BACK" => (ushort)VIRTUAL_KEY.VK_BACK,
            "DELETE" or "DEL" => (ushort)VIRTUAL_KEY.VK_DELETE,
            "INSERT" or "INS" => (ushort)VIRTUAL_KEY.VK_INSERT,
            "HOME" => (ushort)VIRTUAL_KEY.VK_HOME,
            "END" => (ushort)VIRTUAL_KEY.VK_END,
            "PAGEUP" or "PGUP" => (ushort)VIRTUAL_KEY.VK_PRIOR,
            "PAGEDOWN" or "PGDN" => (ushort)VIRTUAL_KEY.VK_NEXT,
            "UP" => (ushort)VIRTUAL_KEY.VK_UP,
            "DOWN" => (ushort)VIRTUAL_KEY.VK_DOWN,
            "LEFT" => (ushort)VIRTUAL_KEY.VK_LEFT,
            "RIGHT" => (ushort)VIRTUAL_KEY.VK_RIGHT,
            _ => 0
        };
    }

    #endregion
}
