using Hermes.Menu;

namespace Hermes.Blazor;

/// <summary>
/// Provides menu structure for Blazor components to render menus in HTML.
/// Used on Windows/Linux where menus are rendered in the custom titlebar.
/// </summary>
public interface IHermesMenuProvider
{
    /// <summary>
    /// Gets all top-level menus.
    /// </summary>
    IReadOnlyList<MenuData> Menus { get; }

    /// <summary>
    /// Invoked when the menu structure changes (items added/removed/modified).
    /// </summary>
    event Action? MenusChanged;

    /// <summary>
    /// Triggers a menu item click, routing through NativeMenuBar.ItemClicked.
    /// </summary>
    /// <param name="itemId">The item ID to click.</param>
    void InvokeItemClick(string itemId);
}

/// <summary>
/// Represents a top-level menu for rendering.
/// </summary>
public sealed class MenuData
{
    /// <summary>
    /// The display label for this menu.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// All items in this menu (including separators).
    /// </summary>
    public required IReadOnlyList<MenuItemData> Items { get; init; }

    /// <summary>
    /// All submenus in this menu.
    /// </summary>
    public required IReadOnlyList<MenuData> Submenus { get; init; }
}

/// <summary>
/// Represents a menu item for rendering.
/// </summary>
public sealed class MenuItemData
{
    /// <summary>
    /// The unique identifier for this item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The display label for this item.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Whether this item is currently enabled.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Whether this item is currently checked.
    /// </summary>
    public required bool IsChecked { get; init; }

    /// <summary>
    /// The accelerator display string (e.g., "Ctrl+S").
    /// </summary>
    public string? AcceleratorText { get; init; }

    /// <summary>
    /// Whether this is a separator item.
    /// </summary>
    public bool IsSeparator { get; init; }
}

/// <summary>
/// Implementation of IHermesMenuProvider that wraps NativeMenuBar.
/// </summary>
internal sealed class HermesMenuProvider : IHermesMenuProvider
{
    private readonly NativeMenuBar _menuBar;
    private List<MenuData>? _cachedMenus;

    public HermesMenuProvider(NativeMenuBar menuBar)
    {
        _menuBar = menuBar;
    }

    public IReadOnlyList<MenuData> Menus
    {
        get
        {
            // Build menu data from NativeMenuBar structure
            // This uses reflection to access the internal _menus dictionary
            // A cleaner approach would be to add a public method to NativeMenuBar
            _cachedMenus ??= BuildMenuData();
            return _cachedMenus;
        }
    }

    public event Action? MenusChanged;

    public void InvokeItemClick(string itemId)
    {
        // Invoke the ItemClicked event on NativeMenuBar
        // Events can only be invoked from within the declaring class,
        // so we use reflection to access the backing field
        var eventField = typeof(NativeMenuBar).GetField(nameof(NativeMenuBar.ItemClicked),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var itemClickedDelegate = eventField?.GetValue(_menuBar) as Action<string>;
        itemClickedDelegate?.Invoke(itemId);
    }

    /// <summary>
    /// Call this to invalidate the cached menu data and notify listeners.
    /// </summary>
    internal void InvalidateCache()
    {
        _cachedMenus = null;
        MenusChanged?.Invoke();
    }

    private List<MenuData> BuildMenuData()
    {
        var result = new List<MenuData>();

        // Access the internal _menus dictionary via reflection
        // This is necessary because NativeMenuBar doesn't expose a public enumeration
        var menusField = typeof(NativeMenuBar).GetField("_menus",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (menusField?.GetValue(_menuBar) is Dictionary<string, NativeMenu> menus)
        {
            foreach (var kvp in menus)
            {
                // Skip the app menu (it's handled differently)
                if (kvp.Key == NativeMenuBar.AppMenuLabel)
                    continue;

                result.Add(ConvertMenu(kvp.Value));
            }
        }

        return result;
    }

    private static MenuData ConvertMenu(NativeMenu menu)
    {
        return new MenuData
        {
            Label = menu.Label,
            Items = menu.Items.Select(ConvertItem).ToList(),
            Submenus = menu.Submenus.Select(ConvertMenu).ToList()
        };
    }

    private static MenuItemData ConvertItem(NativeMenuItem item)
    {
        return new MenuItemData
        {
            Id = item.Id,
            Label = item.Label,
            IsEnabled = item.IsEnabled,
            IsChecked = item.IsChecked,
            AcceleratorText = item.Accelerator?.ToPlatformString(),
            IsSeparator = false
        };
    }
}
