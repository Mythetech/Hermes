// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Storage;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests;

public class WindowStatePersistenceTests
{
    /// <summary>
    /// Regression test: closing a window without resizing should save the configured size,
    /// not 0x0 (which previously happened because CaptureNormalState ran before Show).
    /// </summary>
    [Fact]
    public void CloseWithoutResize_SavesConfiguredSize()
    {
        var key = $"test-no-resize-{Guid.NewGuid():N}";

        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test Window")
            .SetSize(800, 600)
            .SetPosition(100, 200)
            .RememberWindowState(key);

        testWindow.Show();
        // Close immediately — no resize or move events
        testWindow.Close();

        Assert.True(WindowStateStore.Instance.TryGetState(key, out var state));
        Assert.NotNull(state);
        Assert.Equal(800, state.Width);
        Assert.Equal(600, state.Height);
    }

    /// <summary>
    /// Degenerate saved state (1x1) should be ignored on restore, falling back to defaults.
    /// </summary>
    [Fact]
    public void Restore_IgnoresDegenerateState()
    {
        var key = $"test-degenerate-{Guid.NewGuid():N}";

        // Pre-populate the store with a degenerate 1x1 state
        WindowStateStore.Instance.SaveState(key, new WindowState
        {
            X = 50, Y = 50, Width = 1, Height = 1, IsMaximized = false
        });

        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test Window")
            .SetSize(1024, 768)
            .RememberWindowState(key);

        testWindow.Show();

        // The backend should have the configured defaults, not the degenerate saved state
        Assert.Equal((1024, 768), testWindow.Backend.Size);
    }

    /// <summary>
    /// Degenerate state should not be saved to disk — the save should be skipped entirely.
    /// </summary>
    [Fact]
    public void Save_RejectsDegenerateSize()
    {
        var key = $"test-reject-save-{Guid.NewGuid():N}";

        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test Window")
            .SetSize(800, 600)
            .SetPosition(100, 200)
            .RememberWindowState(key);

        testWindow.Show();

        // Simulate a resize to a degenerate size, then close
        testWindow.SimulateResize(0, 0);
        testWindow.Close();

        // The store should either have no state or have the original valid size
        if (WindowStateStore.Instance.TryGetState(key, out var state))
        {
            Assert.True(state!.Width >= 10, $"Expected width >= 10 but got {state.Width}");
            Assert.True(state.Height >= 10, $"Expected height >= 10 but got {state.Height}");
        }
    }

    /// <summary>
    /// Valid saved state should restore correctly.
    /// </summary>
    [Fact]
    public void Restore_AppliesValidSavedState()
    {
        var key = $"test-valid-restore-{Guid.NewGuid():N}";

        // Pre-populate with valid state
        WindowStateStore.Instance.SaveState(key, new WindowState
        {
            X = 150, Y = 250, Width = 1280, Height = 720, IsMaximized = false
        });

        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test Window")
            .SetSize(800, 600)
            .RememberWindowState(key);

        testWindow.Show();

        Assert.Equal((1280, 720), testWindow.Backend.Size);
        Assert.Equal((150, 250), testWindow.Backend.Position);
    }

    /// <summary>
    /// Closing a maximized window should save the pre-maximize dimensions, not 0x0.
    /// </summary>
    [Fact]
    public void CloseWhileMaximized_SavesPreMaximizeDimensions()
    {
        var key = $"test-maximized-close-{Guid.NewGuid():N}";

        using var testWindow = new TestableHermesWindow()
            .SetTitle("Test Window")
            .SetSize(800, 600)
            .SetPosition(100, 200)
            .RememberWindowState(key);

        testWindow.Show();
        testWindow.SimulateMaximize();
        testWindow.Close();

        Assert.True(WindowStateStore.Instance.TryGetState(key, out var state));
        Assert.NotNull(state);
        Assert.True(state.IsMaximized);
        // Should have the pre-maximize dimensions, not 0x0
        Assert.Equal(800, state.Width);
        Assert.Equal(600, state.Height);
    }
}
