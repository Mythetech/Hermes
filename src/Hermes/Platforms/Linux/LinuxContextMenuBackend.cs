using System.Runtime.Versioning;
using Hermes.Abstractions;
using Gtk;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxContextMenuBackend : IContextMenuBackend
{
    private readonly Gtk.Window _window;
    private readonly Gtk.Menu _menu;

    private readonly Dictionary<string, MenuItem> _itemsByCommandId = new();
    private readonly Dictionary<MenuItem, string> _commandIdByItem = new();

    private bool _disposed;

    public event Action<string>? MenuItemClicked;

    internal LinuxContextMenuBackend(Gtk.Window window)
    {
        _window = window;
        _menu = new Menu();
    }

    public void AddItem(string itemId, string label, string? accelerator = null)
    {
        var menuItem = new MenuItem(label);
        _itemsByCommandId[itemId] = menuItem;
        _commandIdByItem[menuItem] = itemId;

        menuItem.Activated += OnMenuItemActivated;

        _menu.Append(menuItem);
        menuItem.ShowAll();
    }

    public void AddSeparator()
    {
        var separator = new SeparatorMenuItem();
        _menu.Append(separator);
        separator.ShowAll();
    }

    public void RemoveItem(string itemId)
    {
        if (!_itemsByCommandId.TryGetValue(itemId, out var menuItem))
            return;

        _menu.Remove(menuItem);
        menuItem.Destroy();

        _itemsByCommandId.Remove(itemId);
        _commandIdByItem.Remove(menuItem);
    }

    public void Clear()
    {
        foreach (var child in _menu.Children)
        {
            _menu.Remove(child);
            child.Destroy();
        }

        _itemsByCommandId.Clear();
        _commandIdByItem.Clear();
    }

    public void SetItemEnabled(string itemId, bool enabled)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            menuItem.Sensitive = enabled;
        }
    }

    public void SetItemChecked(string itemId, bool isChecked)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            if (menuItem is CheckMenuItem checkItem)
            {
                checkItem.Active = isChecked;
            }
        }
    }

    public void SetItemLabel(string itemId, string label)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            if (menuItem.Child is Label labelWidget)
            {
                labelWidget.Text = label;
            }
        }
    }

    public void Show(int x, int y)
    {
        _menu.ShowAll();

        // Popup at the specified location
        // The position func receives the menu's natural size and returns the desired x,y
        _menu.Popup(null, null, (Menu menu, out int menuX, out int menuY, out bool pushIn) =>
        {
            menuX = x;
            menuY = y;
            pushIn = true;
        }, 0, Gtk.Global.CurrentEventTime);
    }

    public void Hide()
    {
        _menu.Popdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var item in _itemsByCommandId.Values)
        {
            item.Activated -= OnMenuItemActivated;
        }

        _menu.Destroy();
    }

    private void OnMenuItemActivated(object? sender, EventArgs args)
    {
        if (sender is MenuItem menuItem && _commandIdByItem.TryGetValue(menuItem, out var commandId))
        {
            MenuItemClicked?.Invoke(commandId);
        }
    }
}
