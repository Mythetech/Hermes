// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Contracts.Plugins;
using Hermes.Plugins;
using Xunit;

namespace Hermes.Tests.Plugins;

public sealed class DesktopClipboardTests
{
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
}
