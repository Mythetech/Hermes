// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.StatusIcon;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests;

public class NativeTrayMenuItemTests
{
    private RecordingStatusIconBackend CreateBackend() => new();

    [Fact]
    public void Label_Set_UpdatesBackend()
    {
        var backend = CreateBackend();
        var item = new NativeTrayMenuItem(backend, "item1", "Original");

        item.Label = "Updated";

        Assert.Contains("SetMenuItemLabel:item1=Updated", backend.Operations);
    }

    [Fact]
    public void Label_SetSameValue_DoesNotUpdateBackend()
    {
        var backend = CreateBackend();
        var item = new NativeTrayMenuItem(backend, "item1", "Same");

        item.Label = "Same";

        Assert.DoesNotContain("SetMenuItemLabel:item1=Same", backend.Operations);
    }

    [Fact]
    public void IsEnabled_Set_UpdatesBackend()
    {
        var backend = CreateBackend();
        var item = new NativeTrayMenuItem(backend, "item1", "Label");

        item.IsEnabled = false;

        Assert.Contains("SetMenuItemEnabled:item1=False", backend.Operations);
    }

    [Fact]
    public void IsChecked_Set_UpdatesBackend()
    {
        var backend = CreateBackend();
        var item = new NativeTrayMenuItem(backend, "item1", "Label");

        item.IsChecked = true;

        Assert.Contains("SetMenuItemChecked:item1=True", backend.Operations);
    }

    [Fact]
    public void WithEnabled_SetsInitialState_WithoutBackendCall()
    {
        var backend = CreateBackend();
        var item = new NativeTrayMenuItem(backend, "item1", "Label");

        item.WithEnabled(false);

        Assert.False(item.IsEnabled);
        Assert.DoesNotContain("SetMenuItemEnabled:item1=False", backend.Operations);
    }

    [Fact]
    public void WithChecked_SetsInitialState_WithoutBackendCall()
    {
        var backend = CreateBackend();
        var item = new NativeTrayMenuItem(backend, "item1", "Label");

        item.WithChecked(true);

        Assert.True(item.IsChecked);
        Assert.DoesNotContain("SetMenuItemChecked:item1=True", backend.Operations);
    }
}

public class NativeTraySubmenuTests
{
    private RecordingStatusIconBackend CreateBackend() => new();

    private NativeTraySubmenu CreateSubmenu(
        RecordingStatusIconBackend backend,
        Dictionary<string, NativeTrayMenuItem>? globalItems = null)
    {
        globalItems ??= new Dictionary<string, NativeTrayMenuItem>();
        return new NativeTraySubmenu(backend, "sub1", "Submenu", globalItems);
    }

    [Fact]
    public void AddItem_RegistersWithBackend()
    {
        var backend = CreateBackend();
        var submenu = CreateSubmenu(backend);

        submenu.AddItem("My Item", "item1");

        Assert.Contains("AddSubmenuItem:sub1/item1=My Item", backend.Operations);
        Assert.Single(submenu.Items);
    }

    [Fact]
    public void AddItem_WithConfigure_AppliesInitialState()
    {
        var backend = CreateBackend();
        var submenu = CreateSubmenu(backend);

        submenu.AddItem("Disabled Item", "item1", item => item.WithEnabled(false));

        Assert.Contains("SetMenuItemEnabled:item1=False", backend.Operations);
    }

    [Fact]
    public void AddSeparator_RegistersWithBackend()
    {
        var backend = CreateBackend();
        var submenu = CreateSubmenu(backend);

        submenu.AddSeparator();

        Assert.Contains("AddSubmenuSeparator:sub1", backend.Operations);
    }

    [Fact]
    public void Clear_RemovesItemsAndUpdatesBackend()
    {
        var backend = CreateBackend();
        var globalItems = new Dictionary<string, NativeTrayMenuItem>();
        var submenu = CreateSubmenu(backend, globalItems);

        submenu.AddItem("Item A", "itemA");
        submenu.AddItem("Item B", "itemB");
        submenu.Clear();

        Assert.Contains("ClearSubmenu:sub1", backend.Operations);
        Assert.Empty(submenu.Items);
        Assert.DoesNotContain("itemA", globalItems.Keys);
        Assert.DoesNotContain("itemB", globalItems.Keys);
    }
}

public class NativeTrayMenuTests
{
    private RecordingStatusIconBackend CreateBackend() => new();

    private NativeTrayMenu CreateMenu(RecordingStatusIconBackend backend)
        => new NativeTrayMenu(backend);

    [Fact]
    public void AddItem_RegistersWithBackend()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        menu.AddItem("My Item", "item1");

        Assert.Contains("AddMenuItem:item1=My Item", backend.Operations);
    }

    [Fact]
    public void AddItem_WithConfigure_AppliesInitialState()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        menu.AddItem("Disabled Item", "item1", item => item.WithEnabled(false));

        Assert.Contains("SetMenuItemEnabled:item1=False", backend.Operations);
    }

    [Fact]
    public void AddSeparator_RegistersWithBackend()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        menu.AddSeparator();

        Assert.Contains("AddMenuSeparator", backend.Operations);
    }

    [Fact]
    public void AddSubmenu_RegistersWithBackend()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        menu.AddSubmenu("My Submenu", "sub1");

        Assert.Contains("AddSubmenu:sub1=My Submenu", backend.Operations);
    }

    [Fact]
    public void Indexer_ReturnsItem()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);
        menu.AddItem("My Item", "item1");

        var item = menu["item1"];

        Assert.Equal("item1", item.Id);
        Assert.Equal("My Item", item.Label);
    }

    [Fact]
    public void Indexer_ThrowsForMissingItem()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        Assert.Throws<KeyNotFoundException>(() => menu["missing"]);
    }

    [Fact]
    public void TryGetItem_ReturnsFalseForMissing()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        var result = menu.TryGetItem("missing", out var item);

        Assert.False(result);
        Assert.Null(item);
    }

    [Fact]
    public void RemoveItem_RemovesFromBackend()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);
        menu.AddItem("My Item", "item1");

        menu.RemoveItem("item1");

        Assert.Contains("RemoveMenuItem:item1", backend.Operations);
        Assert.False(menu.TryGetItem("item1", out _));
    }

    [Fact]
    public void Clear_ClearsAllItems()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);
        menu.AddItem("Item A", "itemA");
        menu.AddItem("Item B", "itemB");

        menu.Clear();

        Assert.Contains("ClearMenu", backend.Operations);
        Assert.False(menu.TryGetItem("itemA", out _));
        Assert.False(menu.TryGetItem("itemB", out _));
    }

    [Fact]
    public void ItemClicked_ForwardsFromBackend()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);
        menu.AddItem("My Item", "item1");

        string? clickedId = null;
        menu.ItemClicked += id => clickedId = id;

        backend.SimulateMenuItemClick("item1");

        Assert.Equal("item1", clickedId);
    }

    [Fact]
    public void FluentChaining_Works()
    {
        var backend = CreateBackend();
        var menu = CreateMenu(backend);

        var result = menu
            .AddItem("Item A", "itemA")
            .AddSeparator()
            .AddItem("Item B", "itemB")
            .AddSubmenu("Sub", "sub1")
            .RemoveItem("itemA")
            .Clear();

        Assert.Same(menu, result);
    }
}

public class NativeStatusIconTests
{
    private RecordingStatusIconBackend CreateBackend() => new();

    private NativeStatusIcon CreateIcon(RecordingStatusIconBackend backend)
        => new NativeStatusIcon(backend);

    [Fact]
    public void SetIcon_RecordsOperation()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.SetIcon("icon.png");

        Assert.Contains("SetIcon:icon.png", backend.Operations);
    }

    [Fact]
    public void SetIconFromStream_RecordsOperation()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        using var stream = new MemoryStream();
        icon.SetIconFromStream(stream);

        Assert.Contains("SetIconFromStream", backend.Operations);
    }

    [Fact]
    public void SetTooltip_RecordsOperation()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.SetTooltip("My App");

        Assert.Contains("SetTooltip:My App", backend.Operations);
    }

    [Fact]
    public void SetMenu_ConfiguresMenu()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.SetMenu(menu => menu.AddItem("Quit", "tray.quit"));

        Assert.NotNull(icon.Menu);
        Assert.Contains("AddMenuItem:tray.quit=Quit", backend.Operations);
    }

    [Fact]
    public void Show_InitializesAndShows()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();

        Assert.Contains("Initialize", backend.Operations);
        Assert.Contains("Show", backend.Operations);
    }

    [Fact]
    public void Hide_HidesIcon()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();
        icon.Hide();

        Assert.Contains("Hide", backend.Operations);
    }

    [Fact]
    public void Tooltip_PostShow_UpdatesBackend()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.SetTooltip("Before");
        icon.Show();

        // Clear operations count by checking the new tooltip is recorded after show
        icon.Tooltip = "After";

        Assert.Contains("SetTooltip:After", backend.Operations);
    }

    [Fact]
    public void IsVisible_PostShow_TogglesVisibility()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();
        Assert.True(icon.IsVisible);

        icon.IsVisible = false;
        Assert.False(icon.IsVisible);
        Assert.Contains("Hide", backend.Operations);
    }

    [Fact]
    public void IsVisible_AfterHide_ReturnsFalse()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();
        Assert.True(icon.IsVisible);

        icon.Hide();
        Assert.False(icon.IsVisible);
    }

    [Fact]
    public void OnClicked_HandlerCalled()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        var called = false;
        icon.OnClicked(() => called = true);
        icon.Show();

        backend.SimulateClick();

        Assert.True(called);
    }

    [Fact]
    public void OnDoubleClicked_HandlerCalled()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        var called = false;
        icon.OnDoubleClicked(() => called = true);
        icon.Show();

        backend.SimulateDoubleClick();

        Assert.True(called);
    }

    [Fact]
    public void Dispose_CleansUp()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();
        icon.Dispose();

        Assert.Contains("Hide", backend.Operations);
        Assert.Contains("Dispose", backend.Operations);
    }

    [Fact]
    public void Dispose_DoubleDispose_IsSafe()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();
        icon.Dispose();
        icon.Dispose(); // Should not throw

        Assert.Single(backend.Operations, op => op == "Dispose");
    }

    [Fact]
    public void Show_AfterDispose_ThrowsObjectDisposedException()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        icon.Show();
        icon.Dispose();

        Assert.Throws<ObjectDisposedException>(() => icon.Show());
    }

    [Fact]
    public void FluentChaining_Works()
    {
        var backend = CreateBackend();
        var icon = CreateIcon(backend);

        var result = icon
            .SetIcon("icon.png")
            .SetTooltip("My App")
            .SetMenu(menu => menu.AddItem("Quit", "tray.quit"))
            .OnClicked(() => { })
            .OnDoubleClicked(() => { });

        Assert.Same(icon, result);
    }
}
