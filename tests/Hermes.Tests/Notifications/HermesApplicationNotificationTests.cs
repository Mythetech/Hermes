// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Notifications;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Notifications;

// HermesApplication holds process-wide static state, so these tests must not run alongside each other.
[Collection("HermesApplicationNotifications")]
public sealed class HermesApplicationNotificationTests : IDisposable
{
    private readonly List<NativeNotificationOptions> _factoryCalls = new();
    private RecordingNotificationBackend? _lastBackend;

    public HermesApplicationNotificationTests()
    {
        HermesApplication.ResetNotificationsForTesting();
        HermesApplication.SetNotificationBackendFactoryForTesting(options =>
        {
            _factoryCalls.Add(options);
            _lastBackend = new RecordingNotificationBackend();
            return _lastBackend;
        });
    }

    public void Dispose()
    {
        HermesApplication.SetNotificationBackendFactoryForTesting(null);
        HermesApplication.ResetNotificationsForTesting();
    }

    [Fact]
    public void Notifications_IsLazyAndCached()
    {
        var first = HermesApplication.Notifications;
        var second = HermesApplication.Notifications;

        Assert.Same(first, second);
        Assert.Single(_factoryCalls);
    }

    [Fact]
    public void ConfigureNotifications_BeforeAccess_IsPassedToFactoryResolved()
    {
        HermesApplication.ConfigureNotifications(new NativeNotificationOptions { AppId = "Mythetech.Demo", IconPath = "/tmp/i.png" });

        _ = HermesApplication.Notifications;

        var options = Assert.Single(_factoryCalls);
        Assert.Equal("Mythetech.Demo", options.AppId);
        Assert.Equal("Mythetech.Demo", options.DisplayName);
        Assert.Equal("/tmp/i.png", options.IconPath);
    }

    [Fact]
    public void Notifications_WithoutConfigure_DefaultsAppIdToEntryAssemblyOrFallback()
    {
        _ = HermesApplication.Notifications;

        var options = Assert.Single(_factoryCalls);
        Assert.False(string.IsNullOrWhiteSpace(options.AppId));
        Assert.Equal(options.AppId, options.DisplayName);
    }

    [Fact]
    public void ConfigureNotifications_AfterAccess_Throws()
    {
        _ = HermesApplication.Notifications;

        Assert.Throws<InvalidOperationException>(() => HermesApplication.ConfigureNotifications(new NativeNotificationOptions()));
    }

    [Fact]
    public void ConfigureNotifications_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => HermesApplication.ConfigureNotifications(null!));
    }

    [Fact]
    public void Shutdown_DisposesCenter_AndNextAccessCreatesNew()
    {
        var first = HermesApplication.Notifications;
        var firstBackend = _lastBackend!;

        HermesApplication.Shutdown();
        var second = HermesApplication.Notifications;

        Assert.True(firstBackend.IsDisposed);
        Assert.NotSame(first, second);
        Assert.Equal(2, _factoryCalls.Count);
    }

    [Fact]
    public void Shutdown_AllowsConfigureAgain()
    {
        _ = HermesApplication.Notifications;
        HermesApplication.Shutdown();

        HermesApplication.ConfigureNotifications(new NativeNotificationOptions { AppId = "Second" });
        _ = HermesApplication.Notifications;

        Assert.Equal("Second", _factoryCalls[1].AppId);
    }
}
