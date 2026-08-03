// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Menu;
using Xunit;

namespace Hermes.Tests.Web;

public class HermesMenuProviderTests
{
    [Fact]
    public void Constructor_DoesNotInvokeFactory()
    {
        var invoked = false;

        _ = new HermesMenuProvider(() => { invoked = true; return CreateMenuBar(); });

        Assert.False(invoked);
    }

    [Fact]
    public void Menus_InvokesFactoryOnce()
    {
        var count = 0;
        var provider = new HermesMenuProvider(() => { count++; return CreateMenuBar(); });

        _ = provider.Menus;
        _ = provider.Menus;

        Assert.Equal(1, count);
    }

    [Fact]
    public void InvokeItemClick_ResolvesMenuBarLazily()
    {
        var count = 0;
        var provider = new HermesMenuProvider(() => { count++; return CreateMenuBar(); });

        provider.InvokeItemClick("some-item");

        Assert.Equal(1, count);
    }

    private static NativeMenuBar CreateMenuBar()
    {
        return new NativeMenuBar(new FakeMenuBackend());
    }

    private sealed class FakeMenuBackend : IMenuBackend
    {
        public void AddMenu(string label, int insertIndex = -1) { }
        public void RemoveMenu(string label) { }
        public void AddItem(string menuLabel, string itemId, string itemLabel, string? accelerator = null) { }
        public void InsertItem(string menuLabel, string afterItemId, string itemId, string itemLabel, string? accelerator = null) { }
        public void RemoveItem(string menuLabel, string itemId) { }
        public void AddSeparator(string menuLabel) { }
        public void SetItemEnabled(string menuLabel, string itemId, bool enabled) { }
        public void SetItemChecked(string menuLabel, string itemId, bool isChecked) { }
        public void SetItemLabel(string menuLabel, string itemId, string label) { }
        public void SetItemAccelerator(string menuLabel, string itemId, string accelerator) { }
        public event Action<string>? MenuItemClicked { add { } remove { } }
        public void AddSubmenu(string menuPath, string submenuLabel) { }
        public void AddSubmenuItem(string menuPath, string itemId, string itemLabel, string? accelerator = null) { }
        public void AddSubmenuSeparator(string menuPath) { }
        public string AppName => "TestApp";
        public void AddAppMenuItem(string itemId, string itemLabel, string? accelerator = null, string? position = null) { }
        public void AddAppMenuSeparator(string? position = null) { }
        public void RemoveAppMenuItem(string itemId) { }
    }
}
