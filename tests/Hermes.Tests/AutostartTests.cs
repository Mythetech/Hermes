// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Xunit;

namespace Hermes.Tests;

public sealed class AutostartTests
{
    [Fact]
    public void SetEnabled_NullAppId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => Autostart.SetEnabled(null!, true));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetEnabled_EmptyAppId_ThrowsArgumentException(string appId)
    {
        Assert.Throws<ArgumentException>(() => Autostart.SetEnabled(appId, true));
    }

    [Fact]
    public void GetIsEnabled_NullAppId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => Autostart.GetIsEnabled(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetIsEnabled_EmptyAppId_ThrowsArgumentException(string appId)
    {
        Assert.Throws<ArgumentException>(() => Autostart.GetIsEnabled(appId));
    }
}
