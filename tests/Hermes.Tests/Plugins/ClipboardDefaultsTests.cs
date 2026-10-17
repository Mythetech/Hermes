// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.Contracts.Plugins;
using Xunit;

namespace Hermes.Tests.Plugins;

public sealed class ClipboardDefaultsTests
{
    private sealed class TextOnlyClipboard : IClipboard
    {
        public string? Value { get; set; }

        public Task SetTextAsync(string text, CancellationToken ct = default)
        {
            Value = text;
            return Task.CompletedTask;
        }

        public Task<string?> GetTextAsync(CancellationToken ct = default) => Task.FromResult(Value);
    }

    [Fact]
    public async Task GetFormatsAsync_ReportsTextWhenPresent()
    {
        IClipboard clipboard = new TextOnlyClipboard { Value = "a" };
        IClipboard empty = new TextOnlyClipboard();

        Assert.Equal(HermesDataFormats.Text, await clipboard.GetFormatsAsync());
        Assert.Equal(HermesDataFormats.None, await empty.GetFormatsAsync());
    }

    [Fact]
    public async Task ReadAsync_FillsOnlyText()
    {
        IClipboard clipboard = new TextOnlyClipboard { Value = "a" };

        var withText = await clipboard.ReadAsync(HermesDataFormats.Text | HermesDataFormats.Html);
        var withoutText = await clipboard.ReadAsync(HermesDataFormats.Html);

        Assert.Equal("a", withText.Text);
        Assert.Null(withText.Html);
        Assert.True(withoutText.IsEmpty);
    }

    [Fact]
    public async Task WriteAsync_TextOnly_CallsSetText()
    {
        var clipboard = new TextOnlyClipboard();

        await ((IClipboard)clipboard).WriteAsync(new HermesDataPackage { Text = "a" });

        Assert.Equal("a", clipboard.Value);
    }

    [Fact]
    public async Task WriteAsync_RichPackage_ThrowsNotSupported()
    {
        IClipboard clipboard = new TextOnlyClipboard();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            clipboard.WriteAsync(new HermesDataPackage { Text = "a", Html = "<b>a</b>" }));
    }

    [Fact]
    public void Changed_DefaultAccessorsDoNothing()
    {
        IClipboard clipboard = new TextOnlyClipboard();
        Action handler = () => { };

        clipboard.Changed += handler;
        clipboard.Changed -= handler;
    }
}
