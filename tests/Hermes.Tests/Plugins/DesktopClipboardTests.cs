// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.Contracts.Plugins;
using Hermes.Plugins;
using Hermes.Testing;
using Hermes.Tests.DataTransfer;
using Xunit;

namespace Hermes.Tests.Plugins;

[Collection(ClipboardCollection.Name)]
public sealed class DesktopClipboardTests : IDisposable
{
    private readonly RecordingClipboardBackend _backend = new();

    public DesktopClipboardTests() => Clipboard.SetBackendForTesting(_backend);

    public void Dispose()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        Clipboard.SetBackendForTesting(null);
    }

    [Fact]
    public async Task SetTextAsync_Null_ThrowsArgumentNullException()
    {
        IClipboard clipboard = new DesktopClipboard();
        await Assert.ThrowsAsync<ArgumentNullException>(() => clipboard.SetTextAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetTextAsync_Empty_ThrowsArgumentException(string text)
    {
        IClipboard clipboard = new DesktopClipboard();
        await Assert.ThrowsAsync<ArgumentException>(() => clipboard.SetTextAsync(text));
    }

    [Fact]
    public async Task WriteAsync_ThenReadAsync_UsesTheStaticClipboard()
    {
        IClipboard clipboard = new DesktopClipboard();

        await clipboard.WriteAsync(new HermesDataPackage { Text = "a", Html = "<b>a</b>" });
        var result = await clipboard.ReadAsync(HermesDataFormats.Html);

        Assert.Equal("<b>a</b>", result.Html);
        Assert.Equal(HermesDataFormats.Text | HermesDataFormats.Html, await clipboard.GetFormatsAsync());
    }

    [Fact]
    public void Changed_SubscribesThroughTheStaticClipboard()
    {
        // xUnit v2 runs sync tests under its own context, which Changed would capture and post to.
        SynchronizationContext.SetSynchronizationContext(null);
        IClipboard clipboard = new DesktopClipboard();
        var calls = 0;
        Action handler = () => calls++;

        clipboard.Changed += handler;
        _backend.RaiseChanged();
        clipboard.Changed -= handler;

        Assert.Equal(1, calls);
        Assert.False(_backend.IsMonitoring);
    }
}
