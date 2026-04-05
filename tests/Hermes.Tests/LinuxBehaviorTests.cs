// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Menu;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests;

/// <summary>
/// Tests for Linux-specific behavior and platform divergence.
/// These validate the managed layer; integration tests cover the real GTK backend.
/// </summary>
public class LinuxCustomTitleBarTests
{
    [Fact]
    public void CustomTitleBar_DoesNotImply_Chromeless()
    {
        // CustomTitleBar and Chromeless are independent options at the options level.
        // On Linux, the native C code handles CustomTitleBar by removing decorations,
        // but the managed options remain independent.
        var options = new HermesWindowOptions { CustomTitleBar = true };

        Assert.True(options.CustomTitleBar);
        Assert.False(options.Chromeless);
    }

    [Fact]
    public void CustomTitleBar_WithRecordingBackend_SetsIsCustomTitleBarActive()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Titlebar Test")
            .SetSize(800, 600)
            .SetCustomTitleBar(true);

        testWindow.Show();

        var opts = testWindow.Backend.InitialOptions;
        Assert.NotNull(opts);
        Assert.True(opts!.CustomTitleBar);
        Assert.True(testWindow.Backend.IsCustomTitleBarActive);
    }

    [Fact]
    public void NoCustomTitleBar_IsCustomTitleBarActive_IsFalse()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Default Test")
            .SetSize(800, 600);

        testWindow.Show();

        Assert.False(testWindow.Backend.IsCustomTitleBarActive);
    }

    [Fact]
    public void Chromeless_StillWorks_Independently()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTitle("Chromeless Test")
            .SetSize(800, 600)
            .SetChromeless(true);

        testWindow.Show();

        var opts = testWindow.Backend.InitialOptions;
        Assert.NotNull(opts);
        Assert.True(opts!.Chromeless);
        Assert.False(opts.CustomTitleBar);
    }
}

/// <summary>
/// Tests for the menu rebuild pattern (remove + re-add) that plugins use.
/// Validates the managed NativeMenuBar layer handles this correctly.
/// </summary>
public class MenuRebuildTests
{
    [Fact]
    public void RemoveMenu_ThenAddMenu_ProducesOneMenu()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);

        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View Plugins", "plugins.view");
        });

        menuBar.RemoveMenu("Plugins");

        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View Plugins", "plugins.view");
            menu.AddItem("Import Plugin...", "plugins.import");
        });

        // Backend should have exactly one active "Plugins" menu
        Assert.Single(backend.ActiveMenus, m => m == "Plugins");
    }

    [Fact]
    public void RepeatedRebuild_DoesNotAccumulateMenus()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);

        // Simulate what Siren does: initial add, then multiple rebuilds
        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View Plugins", "plugins.view");
        });

        for (int i = 0; i < 5; i++)
        {
            menuBar.RemoveMenu("Plugins");
            menuBar.AddMenu("Plugins", menu =>
            {
                menu.AddItem("View Plugins", "plugins.view");
            });
        }

        Assert.Single(backend.ActiveMenus, m => m == "Plugins");
        // 1 initial + 5 rebuilds = 6 total AddMenu calls
        Assert.Equal(6, backend.AddMenuCallCount);
        Assert.Equal(5, backend.RemoveMenuCallCount);
    }

    [Fact]
    public void RemoveMenu_BackendReceives_RemoveBeforeAdd()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);

        menuBar.AddMenu("Plugins", menu => { });
        menuBar.RemoveMenu("Plugins");
        menuBar.AddMenu("Plugins", menu => { });

        var menuOps = backend.Operations
            .Where(o => o.Contains("Menu:Plugins"))
            .ToList();

        Assert.Equal(3, menuOps.Count);
        Assert.Equal("AddMenu:Plugins", menuOps[0]);
        Assert.Equal("RemoveMenu:Plugins", menuOps[1]);
        Assert.Equal("AddMenu:Plugins", menuOps[2]);
    }

    [Fact]
    public void AddMenu_WithSameLabel_Throws()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);

        menuBar.AddMenu("Plugins", menu => { });

        Assert.Throws<InvalidOperationException>(() =>
            menuBar.AddMenu("Plugins", menu => { }));
    }

    [Fact]
    public void RemoveMenu_UnregistersItems_SoRebuildCanReuse()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);

        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View", "plugins.view");
        });

        Assert.True(menuBar.ContainsItem("plugins.view"));

        menuBar.RemoveMenu("Plugins");

        Assert.False(menuBar.ContainsItem("plugins.view"));

        // Re-add with the same item ID should work
        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View", "plugins.view");
        });

        Assert.True(menuBar.ContainsItem("plugins.view"));
    }

    [Fact]
    public void MenuItemClick_AfterRebuild_FiresOnNewItems()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);
        var clickedIds = new List<string>();
        menuBar.ItemClicked += id => clickedIds.Add(id);

        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View", "plugins.view");
        });

        menuBar.RemoveMenu("Plugins");
        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View", "plugins.view");
        });

        backend.SimulateClick("plugins.view");

        Assert.Single(clickedIds);
        Assert.Equal("plugins.view", clickedIds[0]);
    }

    [Fact]
    public void MultipleMenus_RebuildOne_OthersUnaffected()
    {
        var backend = new RecordingMenuBackend();
        var menuBar = new NativeMenuBar(backend);

        menuBar.AddMenu("File", menu =>
        {
            menu.AddItem("New", "file.new");
        });

        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View", "plugins.view");
        });

        // Rebuild only Plugins
        menuBar.RemoveMenu("Plugins");
        menuBar.AddMenu("Plugins", menu =>
        {
            menu.AddItem("View", "plugins.view");
            menu.AddItem("Import", "plugins.import");
        });

        Assert.True(menuBar.ContainsMenu("File"));
        Assert.True(menuBar.ContainsMenu("Plugins"));
        Assert.True(menuBar.ContainsItem("file.new"));
        Assert.True(menuBar.ContainsItem("plugins.view"));
        Assert.True(menuBar.ContainsItem("plugins.import"));
    }
}
