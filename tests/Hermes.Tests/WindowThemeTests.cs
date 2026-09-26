// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests;

public class WindowThemeTests
{
    [Fact]
    public void Theme_DefaultsToSystem()
    {
        var options = new HermesWindowOptions();

        Assert.Equal(HermesWindowTheme.System, options.Theme);
    }

    [Fact]
    public void SetTheme_PassesThemeToBackendOnInitialize()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTheme(HermesWindowTheme.Dark);

        testWindow.Show();

        Assert.Equal(HermesWindowTheme.Dark, testWindow.Backend.InitialOptions!.Theme);
    }

    [Fact]
    public void SetTheme_ReturnsSelfForChaining()
    {
        using var testWindow = new TestableHermesWindow();

        var result = testWindow.Window.SetTheme(HermesWindowTheme.Light);

        Assert.Same(testWindow.Window, result);
    }

    [Fact]
    public void SetTheme_ThrowsAfterInitialization()
    {
        using var testWindow = new TestableHermesWindow();
        testWindow.Show();

        Assert.Throws<InvalidOperationException>(() =>
            testWindow.Window.SetTheme(HermesWindowTheme.Dark));
    }

    [Fact]
    public void ConfiguredOptions_ThemeReachesBackend()
    {
        using var testWindow = new TestableHermesWindow();

        HermesWindowOptions.ApplyTo(testWindow.Window, new HermesWindowOptions { Theme = HermesWindowTheme.Light });
        testWindow.Show();

        Assert.Equal(HermesWindowTheme.Light, testWindow.Backend.InitialOptions!.Theme);
    }

    [Fact]
    public void Theme_SetBeforeShow_BecomesInitialTheme()
    {
        using var testWindow = new TestableHermesWindow();

        testWindow.Window.Theme = HermesWindowTheme.Light;
        testWindow.Show();

        Assert.Equal(HermesWindowTheme.Light, testWindow.Backend.InitialOptions!.Theme);
        Assert.False(testWindow.Recording.MethodWasCalled(nameof(IHermesWindowBackend.SetTheme)));
    }

    [Fact]
    public void Theme_SetAfterShow_AppliesToBackend()
    {
        using var testWindow = new TestableHermesWindow();
        testWindow.Show();

        testWindow.Window.Theme = HermesWindowTheme.Light;

        Assert.Equal(HermesWindowTheme.Light, LastAppliedTheme(testWindow));
    }

    [Fact]
    public void Theme_SetAfterShow_UpdatesGetter()
    {
        using var testWindow = new TestableHermesWindow();
        testWindow.Show();

        testWindow.Window.Theme = HermesWindowTheme.Dark;

        Assert.Equal(HermesWindowTheme.Dark, testWindow.Window.Theme);
    }

    [Fact]
    public void Theme_SetFromBackgroundThread_IsAppliedOnUiThread()
    {
        using var testWindow = new TestableHermesWindow();
        testWindow.Show();

        var worker = new Thread(() => testWindow.Window.Theme = HermesWindowTheme.Dark);
        worker.Start();
        worker.Join();

        Assert.False(testWindow.Recording.MethodWasCalled(nameof(IHermesWindowBackend.SetTheme)));

        testWindow.Backend.ProcessPending();

        Assert.Equal(HermesWindowTheme.Dark, LastAppliedTheme(testWindow));
    }

    private static object? LastAppliedTheme(TestableHermesWindow testWindow) =>
        testWindow.Recording.MethodCalls
            .Last(call => call.MethodName == nameof(IHermesWindowBackend.SetTheme))
            .Arguments[0];
}
