// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Notifications;
using Hermes.Contracts.Plugins;
using Hermes.Plugins;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Plugins;

[Collection("HermesApplicationNotifications")]
public sealed class DesktopNativeNotificationsTests : IDisposable
{
    private readonly RecordingNotificationBackend _backend = new();

    public DesktopNativeNotificationsTests()
    {
        HermesApplication.ResetNotificationsForTesting();
        HermesApplication.SetNotificationBackendFactoryForTesting(_ => _backend);
    }

    public void Dispose()
    {
        HermesApplication.SetNotificationBackendFactoryForTesting(null);
        HermesApplication.ResetNotificationsForTesting();
    }

    [Fact]
    public async Task ShowAsync_ForwardsToApplicationCenter()
    {
        INativeNotifications notifications = new DesktopNativeNotifications();

        await notifications.ShowAsync(new NativeNotification { Id = "n1", Title = "T" });

        Assert.Equal("n1", Assert.Single(_backend.Shown).Id);
    }

    [Fact]
    public void Clicked_SubscribesThroughToCenter()
    {
        INativeNotifications notifications = new DesktopNativeNotifications();
        NotificationClickedEventArgs? received = null;
        notifications.Clicked += args => received = args;
        notifications.ShowAsync(new NativeNotification { Id = "n1", Title = "T", Tag = "route" }).GetAwaiter().GetResult();

        _backend.RaiseClicked("n1");

        Assert.Equal("route", received?.Tag);
    }

    [Fact]
    public void Clicked_Unsubscribe_StopsDelivery()
    {
        INativeNotifications notifications = new DesktopNativeNotifications();
        var count = 0;
        Action<NotificationClickedEventArgs> handler = _ => count++;
        notifications.Clicked += handler;
        notifications.Clicked -= handler;

        _backend.RaiseClicked("n1");

        Assert.Equal(0, count);
    }

    [Fact]
    public void IsSupportedAndReason_Forward()
    {
        _backend.IsSupported = false;
        _backend.UnsupportedReason = "nope";
        INativeNotifications notifications = new DesktopNativeNotifications();

        Assert.False(notifications.IsSupported);
        Assert.Equal("nope", notifications.UnsupportedReason);
    }

    [Fact]
    public void DismissAndDismissAll_Forward()
    {
        INativeNotifications notifications = new DesktopNativeNotifications();

        notifications.Dismiss("n1");
        notifications.DismissAll();

        Assert.Equal(new[] { "n1" }, _backend.Dismissed);
        Assert.Equal(1, _backend.DismissAllCount);
    }
}
