// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Notifications;
using Xunit;

namespace Hermes.Tests.Notifications;

public sealed class NotificationAppIdTests
{
    [Fact]
    public void Sanitize_ReplacesWhitespaceWithDots()
    {
        Assert.Equal("My.Cool.App", NotificationAppId.Sanitize("My Cool\tApp"));
    }

    [Fact]
    public void Sanitize_TruncatesTo128()
    {
        var longId = new string('a', 200);

        Assert.Equal(128, NotificationAppId.Sanitize(longId).Length);
    }

    [Fact]
    public void Sanitize_LeavesValidIdUntouched()
    {
        Assert.Equal("Mythetech.Siren", NotificationAppId.Sanitize("Mythetech.Siren"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Sanitize_Blank_Throws(string appId)
    {
        Assert.Throws<ArgumentException>(() => NotificationAppId.Sanitize(appId));
    }
}
