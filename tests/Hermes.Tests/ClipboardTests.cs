// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Xunit;

namespace Hermes.Tests;

public sealed class ClipboardTests
{
    [Fact]
    public void SetText_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Clipboard.SetText(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetText_Empty_ThrowsArgumentException(string text)
    {
        Assert.Throws<ArgumentException>(() => Clipboard.SetText(text));
    }
}
