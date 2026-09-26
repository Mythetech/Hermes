// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Notifications;
using Xunit;

namespace Hermes.Tests.Notifications;

public sealed class NativeNotificationTests
{
    [Fact]
    public void Id_DefaultsToUniqueCompactGuid()
    {
        var first = new NativeNotification { Title = "a" };
        var second = new NativeNotification { Title = "b" };

        Assert.Equal(32, first.Id.Length);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Id_CanBeSupplied()
    {
        var notification = new NativeNotification { Id = "custom", Title = "a" };

        Assert.Equal("custom", notification.Id);
    }

    [Fact]
    public void ClickedEventArgs_CarriesIdAndTag()
    {
        var args = new NotificationClickedEventArgs("id1", "page/settings");

        Assert.Equal("id1", args.Id);
        Assert.Equal("page/settings", args.Tag);
    }
}
