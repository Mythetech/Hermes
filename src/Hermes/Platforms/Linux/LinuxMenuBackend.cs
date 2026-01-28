using System.Runtime.Versioning;
using Hermes.Abstractions;
using Gtk;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxMenuBackend : IMenuBackend
{
    private readonly Gtk.Window _window;
    private readonly VBox _mainContainer;
    private readonly Widget _webView;
    private readonly MenuBar _menuBar;
    private readonly AccelGroup _accelGroup;

    private readonly Dictionary<string, Gtk.Menu> _menusByLabel = new();
    private readonly Dictionary<string, Gtk.Menu> _submenusByPath = new();
    private readonly Dictionary<string, MenuItem> _menuItemsByLabel = new();
    private readonly Dictionary<string, MenuItem> _itemsByCommandId = new();
    private readonly Dictionary<MenuItem, string> _commandIdByItem = new();

    private bool _menuBarAttached;
    private Gtk.Menu? _appMenu;
    private MenuItem? _appMenuItem;
    private string? _appName;

    public event Action<string>? MenuItemClicked;

    internal LinuxMenuBackend(Gtk.Window window, VBox mainContainer, Widget webView)
    {
        _window = window;
        _mainContainer = mainContainer;
        _webView = webView;
        _menuBar = new MenuBar();
        _accelGroup = new AccelGroup();
        _window.AddAccelGroup(_accelGroup);
    }

    public void AddMenu(string label, int insertIndex = -1)
    {
        EnsureMenuBarAttached();

        var menu = new Gtk.Menu();
        var menuItem = new MenuItem(label);
        menuItem.Submenu = menu;

        _menusByLabel[label] = menu;
        _menuItemsByLabel[label] = menuItem;

        if (insertIndex < 0)
        {
            _menuBar.Append(menuItem);
        }
        else
        {
            _menuBar.Insert(menuItem, insertIndex);
        }

        menuItem.ShowAll();
    }

    public void RemoveMenu(string label)
    {
        if (!_menusByLabel.TryGetValue(label, out var menu))
            return;

        if (!_menuItemsByLabel.TryGetValue(label, out var menuItem))
            return;

        // Remove all items from tracking
        foreach (var child in menu.Children)
        {
            if (child is MenuItem item && _commandIdByItem.TryGetValue(item, out var cmdId))
            {
                _itemsByCommandId.Remove(cmdId);
                _commandIdByItem.Remove(item);
            }
        }

        _menuBar.Remove(menuItem);
        menuItem.Destroy();

        _menusByLabel.Remove(label);
        _menuItemsByLabel.Remove(label);
    }

    public void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var menu))
            throw new ArgumentException($"Menu '{menuLabel}' not found", nameof(menuLabel));

        var menuItem = new MenuItem(itemLabel);
        _itemsByCommandId[itemId] = menuItem;
        _commandIdByItem[menuItem] = itemId;

        menuItem.Activated += OnMenuItemActivated;

        if (!string.IsNullOrEmpty(accelerator))
        {
            ApplyAccelerator(menuItem, accelerator);
        }

        menu.Append(menuItem);
        menuItem.ShowAll();
    }

    public void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var menu))
            throw new ArgumentException($"Menu '{menuLabel}' not found", nameof(menuLabel));

        if (!_itemsByCommandId.TryGetValue(afterItemId, out var afterItem))
            throw new ArgumentException($"Item '{afterItemId}' not found", nameof(afterItemId));

        var menuItem = new MenuItem(itemLabel);
        _itemsByCommandId[itemId] = menuItem;
        _commandIdByItem[menuItem] = itemId;

        menuItem.Activated += OnMenuItemActivated;

        if (!string.IsNullOrEmpty(accelerator))
        {
            ApplyAccelerator(menuItem, accelerator);
        }

        // Find position of afterItem and insert after it
        var children = menu.Children;
        int position = Array.IndexOf(children, afterItem);
        if (position >= 0)
        {
            menu.Insert(menuItem, position + 1);
        }
        else
        {
            menu.Append(menuItem);
        }

        menuItem.ShowAll();
    }

    public void RemoveItem(string menuLabel, string itemId)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var menu))
            return;

        if (!_itemsByCommandId.TryGetValue(itemId, out var menuItem))
            return;

        menu.Remove(menuItem);
        menuItem.Destroy();

        _itemsByCommandId.Remove(itemId);
        _commandIdByItem.Remove(menuItem);
    }

    public void AddSeparator(string menuLabel)
    {
        if (!_menusByLabel.TryGetValue(menuLabel, out var menu))
            throw new ArgumentException($"Menu '{menuLabel}' not found", nameof(menuLabel));

        var separator = new SeparatorMenuItem();
        menu.Append(separator);
        separator.ShowAll();
    }

    public void SetItemEnabled(string menuLabel, string itemId, bool enabled)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            menuItem.Sensitive = enabled;
        }
    }

    public void SetItemChecked(string menuLabel, string itemId, bool isChecked)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            // For checkable items, we need to use CheckMenuItem
            // If it's already a CheckMenuItem, set its state
            if (menuItem is CheckMenuItem checkItem)
            {
                checkItem.Active = isChecked;
            }
            else
            {
                // Convert to CheckMenuItem if needed
                // This requires removing and re-adding the item
                // For simplicity, we'll just handle the case where it's already a CheckMenuItem
                // Future enhancement: track which items should be checkable
            }
        }
    }

    public void SetItemLabel(string menuLabel, string itemId, string label)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            // Update label - access the label child widget
            if (menuItem.Child is Label labelWidget)
            {
                labelWidget.Text = label;
            }
        }
    }

    public void SetItemAccelerator(string menuLabel, string itemId, string accelerator)
    {
        if (_itemsByCommandId.TryGetValue(itemId, out var menuItem))
        {
            ApplyAccelerator(menuItem, accelerator);
        }
    }

    #region Submenu Operations

    public void AddSubmenu(string menuPath, string submenuLabel)
    {
        var parentMenu = FindMenuByPath(menuPath);
        if (parentMenu is null)
            throw new ArgumentException($"Menu '{menuPath}' not found", nameof(menuPath));

        var submenu = new Gtk.Menu();
        var menuItem = new MenuItem(submenuLabel);
        menuItem.Submenu = submenu;

        parentMenu.Append(menuItem);
        menuItem.ShowAll();

        var fullPath = $"{menuPath}/{submenuLabel}";
        _submenusByPath[fullPath] = submenu;
    }

    public void AddSubmenuItem(string menuPath, string itemId, string itemLabel, string? accelerator = null)
    {
        var menu = FindMenuByPath(menuPath);
        if (menu is null)
            throw new ArgumentException($"Menu '{menuPath}' not found", nameof(menuPath));

        var menuItem = new MenuItem(itemLabel);
        _itemsByCommandId[itemId] = menuItem;
        _commandIdByItem[menuItem] = itemId;

        menuItem.Activated += OnMenuItemActivated;

        if (!string.IsNullOrEmpty(accelerator))
        {
            ApplyAccelerator(menuItem, accelerator);
        }

        menu.Append(menuItem);
        menuItem.ShowAll();
    }

    public void AddSubmenuSeparator(string menuPath)
    {
        var menu = FindMenuByPath(menuPath);
        if (menu is null)
            throw new ArgumentException($"Menu '{menuPath}' not found", nameof(menuPath));

        var separator = new SeparatorMenuItem();
        menu.Append(separator);
        separator.ShowAll();
    }

    #endregion

    #region App Menu Operations

    public string AppName => _appName ??= System.Diagnostics.Process.GetCurrentProcess().ProcessName;

    private Gtk.Menu EnsureAppMenu()
    {
        if (_appMenu is not null)
            return _appMenu;

        EnsureMenuBarAttached();

        _appMenu = new Gtk.Menu();
        _appMenuItem = new MenuItem(AppName);
        _appMenuItem.Submenu = _appMenu;

        // Insert at the beginning (position 0)
        _menuBar.Insert(_appMenuItem, 0);
        _appMenuItem.ShowAll();

        return _appMenu;
    }

    public void AddAppMenuItem(string itemId, string itemLabel, string? accelerator = null, string? position = null)
    {
        var appMenu = EnsureAppMenu();

        var menuItem = new MenuItem(itemLabel);
        _itemsByCommandId[itemId] = menuItem;
        _commandIdByItem[menuItem] = itemId;

        menuItem.Activated += OnMenuItemActivated;

        if (!string.IsNullOrEmpty(accelerator))
        {
            ApplyAccelerator(menuItem, accelerator);
        }

        appMenu.Append(menuItem);
        menuItem.ShowAll();
    }

    public void AddAppMenuSeparator(string? position = null)
    {
        var appMenu = EnsureAppMenu();
        var separator = new SeparatorMenuItem();
        appMenu.Append(separator);
        separator.ShowAll();
    }

    public void RemoveAppMenuItem(string itemId)
    {
        if (_appMenu is null) return;
        if (!_itemsByCommandId.TryGetValue(itemId, out var menuItem)) return;

        _appMenu.Remove(menuItem);
        menuItem.Destroy();

        _itemsByCommandId.Remove(itemId);
        _commandIdByItem.Remove(menuItem);
    }

    #endregion

    private Gtk.Menu? FindMenuByPath(string path)
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

    private void EnsureMenuBarAttached()
    {
        if (_menuBarAttached) return;

        // Restructure: remove webview, add menu bar at top, re-add webview
        _mainContainer.Remove(_webView);
        _mainContainer.PackStart(_menuBar, false, false, 0);
        _mainContainer.PackStart(_webView, true, true, 0);
        _mainContainer.ReorderChild(_menuBar, 0);

        _menuBar.ShowAll();
        _menuBarAttached = true;
    }

    private void ApplyAccelerator(MenuItem menuItem, string accelerator)
    {
        // Parse accelerator string like "Ctrl+S" or "Ctrl+Shift+N"
        var parts = accelerator.Split('+');

        var modifiers = Gdk.ModifierType.None;
        Gdk.Key key = Gdk.Key.VoidSymbol;

        foreach (var part in parts)
        {
            var p = part.Trim().ToLowerInvariant();

            switch (p)
            {
                case "ctrl":
                case "control":
                case "cmd":
                case "command":
                    modifiers |= Gdk.ModifierType.ControlMask;
                    break;
                case "shift":
                    modifiers |= Gdk.ModifierType.ShiftMask;
                    break;
                case "alt":
                    modifiers |= Gdk.ModifierType.Mod1Mask;
                    break;
                case "super":
                case "meta":
                    modifiers |= Gdk.ModifierType.SuperMask;
                    break;
                default:
                    key = ParseKey(part.Trim());
                    break;
            }
        }

        if (key != Gdk.Key.VoidSymbol)
        {
            menuItem.AddAccelerator("activate", _accelGroup,
                new AccelKey(key, modifiers, AccelFlags.Visible));
        }
    }

    private static Gdk.Key ParseKey(string keyStr)
    {
        // Handle single characters
        if (keyStr.Length == 1)
        {
            var c = char.ToLower(keyStr[0]);
            return c switch
            {
                >= 'a' and <= 'z' => (Gdk.Key)((int)Gdk.Key.a + (c - 'a')),
                >= '0' and <= '9' => (Gdk.Key)((int)Gdk.Key.Key_0 + (c - '0')),
                _ => Gdk.Key.VoidSymbol
            };
        }

        // Handle special keys
        return keyStr.ToLowerInvariant() switch
        {
            "f1" => Gdk.Key.F1,
            "f2" => Gdk.Key.F2,
            "f3" => Gdk.Key.F3,
            "f4" => Gdk.Key.F4,
            "f5" => Gdk.Key.F5,
            "f6" => Gdk.Key.F6,
            "f7" => Gdk.Key.F7,
            "f8" => Gdk.Key.F8,
            "f9" => Gdk.Key.F9,
            "f10" => Gdk.Key.F10,
            "f11" => Gdk.Key.F11,
            "f12" => Gdk.Key.F12,
            "enter" or "return" => Gdk.Key.Return,
            "escape" or "esc" => Gdk.Key.Escape,
            "tab" => Gdk.Key.Tab,
            "space" => Gdk.Key.space,
            "backspace" => Gdk.Key.BackSpace,
            "delete" or "del" => Gdk.Key.Delete,
            "insert" or "ins" => Gdk.Key.Insert,
            "home" => Gdk.Key.Home,
            "end" => Gdk.Key.End,
            "pageup" or "pgup" => Gdk.Key.Page_Up,
            "pagedown" or "pgdn" => Gdk.Key.Page_Down,
            "up" => Gdk.Key.Up,
            "down" => Gdk.Key.Down,
            "left" => Gdk.Key.Left,
            "right" => Gdk.Key.Right,
            _ => Gdk.Key.VoidSymbol
        };
    }

    private void OnMenuItemActivated(object? sender, EventArgs args)
    {
        if (sender is MenuItem menuItem && _commandIdByItem.TryGetValue(menuItem, out var commandId))
        {
            MenuItemClicked?.Invoke(commandId);
        }
    }
}
