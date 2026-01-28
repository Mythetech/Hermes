using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsContextMenuBackend : IContextMenuBackend
{
    private readonly HWND _hwnd;
    private readonly HMENU _hMenu;

    private readonly Dictionary<string, uint> _itemIdByCommandId = new();
    private readonly Dictionary<uint, string> _commandIdByItemId = new();
    private uint _nextItemId = 10000; // Start higher to avoid collision with menu bar items

    private bool _disposed;

    public event Action<string>? MenuItemClicked;

    internal WindowsContextMenuBackend(HWND hwnd)
    {
        _hwnd = hwnd;
        _hMenu = PInvoke.CreatePopupMenu();

        if (_hMenu.IsNull)
            throw new InvalidOperationException("Failed to create popup menu");
    }

    public void AddItem(string itemId, string label, string? accelerator = null)
    {
        var id = _nextItemId++;
        _itemIdByCommandId[itemId] = id;
        _commandIdByItemId[id] = itemId;

        var displayLabel = FormatLabelWithAccelerator(label, accelerator);
        PInvoke.AppendMenu(_hMenu, MENU_ITEM_FLAGS.MF_STRING, id, displayLabel);
    }

    public void AddSeparator()
    {
        PInvoke.AppendMenu(_hMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, (string?)null);
    }

    public void RemoveItem(string itemId)
    {
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        PInvoke.RemoveMenu(_hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND);
        _itemIdByCommandId.Remove(itemId);
        _commandIdByItemId.Remove(id);
    }

    public void Clear()
    {
        // Remove all items from the end to avoid index shifting issues
        while (PInvoke.GetMenuItemCount(_hMenu) > 0)
        {
            PInvoke.RemoveMenu(_hMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION);
        }

        _itemIdByCommandId.Clear();
        _commandIdByItemId.Clear();
    }

    public void SetItemEnabled(string itemId, bool enabled)
    {
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        var flags = enabled ? MENU_ITEM_FLAGS.MF_ENABLED : MENU_ITEM_FLAGS.MF_GRAYED;
        PInvoke.EnableMenuItem(_hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | flags);
    }

    public void SetItemChecked(string itemId, bool isChecked)
    {
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        var flags = isChecked ? MENU_ITEM_FLAGS.MF_CHECKED : MENU_ITEM_FLAGS.MF_UNCHECKED;
        PInvoke.CheckMenuItem(_hMenu, id, (uint)(MENU_ITEM_FLAGS.MF_BYCOMMAND | flags));
    }

    public void SetItemLabel(string itemId, string label)
    {
        if (!_itemIdByCommandId.TryGetValue(itemId, out var id))
            return;

        PInvoke.ModifyMenu(_hMenu, id, MENU_ITEM_FLAGS.MF_BYCOMMAND | MENU_ITEM_FLAGS.MF_STRING,
            id, label);
    }

    public void Show(int x, int y)
    {
        // TPM_RETURNCMD makes TrackPopupMenu return the selected item ID instead of posting WM_COMMAND
        var flags = TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN
                  | TRACK_POPUP_MENU_FLAGS.TPM_TOPALIGN
                  | TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD;

        BOOL result;
        unsafe
        {
            result = PInvoke.TrackPopupMenu(_hMenu, flags, x, y, 0, _hwnd, null);
        }

        var selectedId = (uint)result.Value;
        if (selectedId != 0 && _commandIdByItemId.TryGetValue(selectedId, out var commandId))
        {
            MenuItemClicked?.Invoke(commandId);
        }
    }

    public void Hide()
    {
        // Windows popup menus close automatically when an item is selected or user clicks away
        // This method sends a cancel message to close any open popup
        PInvoke.EndMenu();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        PInvoke.DestroyMenu(_hMenu);
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
}
