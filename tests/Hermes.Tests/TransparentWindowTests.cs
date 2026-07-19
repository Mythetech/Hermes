// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests;

public class TransparentWindowTests
{
    [Fact]
    public void Transparent_DefaultsFalse()
    {
        var options = new HermesWindowOptions();

        Assert.False(options.Transparent);
    }

    [Fact]
    public void SetTransparent_StoresInOptions()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTransparent(true);

        testWindow.Show();

        Assert.True(testWindow.Backend.InitialOptions!.Transparent);
    }

    [Fact]
    public void SetTransparent_ReturnsSelfForChaining()
    {
        using var testWindow = new TestableHermesWindow();

        var result = testWindow.SetTransparent(true);

        Assert.Same(testWindow, result);
    }

    [Fact]
    public void SetTransparent_ThrowsAfterInitialization()
    {
        using var testWindow = new TestableHermesWindow();
        testWindow.Show();

        Assert.Throws<InvalidOperationException>(() =>
            testWindow.Window.SetTransparent(true));
    }

    [Fact]
    public void ShowWithLoadingState_UsesTransparentHtml_WhenTransparent()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTransparent(true);

        testWindow.Window.ShowWithLoadingState();

        var html = testWindow.Backend.InitialOptions!.StartHtml;
        Assert.NotNull(html);
        Assert.Contains("background: transparent", html);
        Assert.DoesNotContain("#f5f5f5", html);
    }

    [Fact]
    public void ShowWithLoadingState_UsesDefaultHtml_WhenNotTransparent()
    {
        using var testWindow = new TestableHermesWindow();

        testWindow.Window.ShowWithLoadingState();

        var html = testWindow.Backend.InitialOptions!.StartHtml;
        Assert.NotNull(html);
        Assert.Contains("#f5f5f5", html);
    }

    [Fact]
    public void ShowWithLoadingState_CustomHtml_OverridesTransparent()
    {
        using var testWindow = new TestableHermesWindow()
            .SetTransparent(true);

        testWindow.Window.ShowWithLoadingState("<html>custom</html>");

        var html = testWindow.Backend.InitialOptions!.StartHtml;
        Assert.Equal("<html>custom</html>", html);
    }
}
