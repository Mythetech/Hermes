using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsMenuBackend : IMenuBackend
{
    private readonly HWND _hwnd;
    private readonly HMENU _hMenuBar;

    private readonly Dictionary<string, HMENU> _menusByLabel = new();
    private readonly Dictionary<string, uint> _itemIdByCommandId = new();
    private readonly Dictionary<uint, string> _commandIdByItemId = new();
    private uint _nextItemId = 1000;

    public event Action<string>? MenuItemClicked;

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

        PInvoke.AppendMenu(hMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
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
        PInvoke.CheckMenuItem(hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | flags);
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
}
