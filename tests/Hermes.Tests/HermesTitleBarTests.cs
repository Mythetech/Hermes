// Copyright (c) Mythetech. Licensed under the MIT License.
using Bunit;
using Hermes.Blazor;
using Hermes.Blazor.Components;
using Hermes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests;

public class HermesTitleBarTests : BunitContext
{
    private readonly TestableHermesWindow _testWindow = new();

    public HermesTitleBarTests()
    {
        Services.AddSingleton(_testWindow.Window);
        Services.AddSingleton<IHermesMenuProvider>(new NoMenus());
    }

    [Theory]
    [InlineData(32)]
    [InlineData(38)]
    public void PublishesItsHeight_AsACssVariableOnTheRoot(int height)
    {
        var cut = Render<HermesTitleBar>(parameters => parameters.Add(p => p.Height, height));

        Assert.Contains($":root {{ --hermes-titlebar-height: {height}px; }}", cut.Find("style").TextContent);
    }

    [Fact]
    public void PublishedHeight_FollowsAHeightChange()
    {
        var cut = Render<HermesTitleBar>(parameters => parameters.Add(p => p.Height, 32));

        cut.Render(parameters => parameters.Add(p => p.Height, 44));

        Assert.Contains("--hermes-titlebar-height: 44px;", cut.Find("style").TextContent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _testWindow.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class NoMenus : IHermesMenuProvider
    {
        public IReadOnlyList<MenuData> Menus { get; } = [];

#pragma warning disable CS0067 // never raised: the bar has no menus to change
        public event Action? MenusChanged;
#pragma warning restore CS0067

        public void InvokeItemClick(string itemId)
        {
        }
    }
}
