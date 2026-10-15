// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor;
using Hermes.Contracts.DataTransfer;
using Hermes.Contracts.Plugins;
using Hermes.Plugins;
using Hermes.Testing;
using Hermes.Tests.DataTransfer;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.Tests.Plugins;

[Collection(DragDropCollection.Name)]
public sealed class DesktopDragDropTests
{
    [Fact]
    public async Task ForwardsDroppedAndStartDragAsyncToTheWindow()
    {
        using var testable = new TestableHermesWindow();
        testable.Window.SetDragDrop(true);
        testable.Window.Show();
        IDragDrop dragDrop = new DesktopDragDrop(testable.Window);
        HermesDrop? dropped = null;
        dragDrop.Dropped += drop => dropped = drop;
        var package = new HermesDataPackage { Text = "x" };

        testable.Backend.SimulateDrop(package, 1, 2);
        var result = await dragDrop.StartDragAsync(package);

        Assert.Same(package, dropped?.Data);
        Assert.Equal(HermesDragResult.Dropped, result);
        Assert.Same(package, Assert.Single(testable.Backend.DragsStarted));
    }

    [Fact]
    public void BlazorApps_ResolveIDragDrop()
    {
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        var composition = HermesBlazorAppBuilder.ComposeForTest(builder, new RecordingWindowBackend());

        Assert.IsType<DesktopDragDrop>(composition.ServiceProvider.GetRequiredService<IDragDrop>());
    }
}
