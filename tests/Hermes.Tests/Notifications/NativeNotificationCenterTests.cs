// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Notifications;
using Hermes.Notifications;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Notifications;

public sealed class NativeNotificationCenterTests
{
    private static (NativeNotificationCenter Center, RecordingNotificationBackend Backend, List<string> Warnings) Create(
        Action<RecordingNotificationBackend>? configure = null)
    {
        var backend = new RecordingNotificationBackend();
        configure?.Invoke(backend);
        var warnings = new List<string>();
        var center = new NativeNotificationCenter(backend, warnings.Add);
        return (center, backend, warnings);
    }

    [Fact]
    public async Task ShowAsync_ForwardsFieldsAndId()
    {
        var (center, backend, _) = Create();
        var notification = new NativeNotification { Id = "n1", Title = "T", Body = "B", Silent = true };

        await center.ShowAsync(notification);

        var shown = Assert.Single(backend.Shown);
        Assert.Equal(("n1", "T", "B", (string?)null, true), shown);
    }

    [Fact]
    public async Task ShowAsync_WithExistingIcon_ForwardsPath()
    {
        var iconPath = Path.GetTempFileName();
        try
        {
            var (center, backend, _) = Create();

            await center.ShowAsync(new NativeNotification { Title = "T", IconPath = iconPath });

            Assert.Equal(iconPath, Assert.Single(backend.Shown).IconPath);
        }
        finally
        {
            File.Delete(iconPath);
        }
    }

    [Fact]
    public async Task ShowAsync_MissingIcon_ThrowsBeforeBackend()
    {
        var (center, backend, _) = Create();
        var missing = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.png");

        await Assert.ThrowsAsync<FileNotFoundException>(() => center.ShowAsync(new NativeNotification { Title = "T", IconPath = missing }));

        Assert.Empty(backend.Shown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ShowAsync_BlankTitle_ThrowsBeforeBackend(string title)
    {
        var (center, backend, _) = Create();

        await Assert.ThrowsAsync<ArgumentException>(() => center.ShowAsync(new NativeNotification { Title = title }));

        Assert.Empty(backend.Shown);
    }

    [Fact]
    public async Task ShowAsync_NullNotification_Throws()
    {
        var (center, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(() => center.ShowAsync(null!));
    }

    [Fact]
    public void Clicked_ResolvesTagForKnownId()
    {
        var (center, backend, _) = Create();
        NotificationClickedEventArgs? received = null;
        center.Clicked += args => received = args;
        center.ShowAsync(new NativeNotification { Id = "n1", Title = "T", Tag = "page/settings" }).GetAwaiter().GetResult();

        backend.RaiseClicked("n1");

        Assert.NotNull(received);
        Assert.Equal("n1", received.Id);
        Assert.Equal("page/settings", received.Tag);
    }

    [Fact]
    public void Clicked_UnknownId_HasNullTag()
    {
        var (center, backend, _) = Create();
        NotificationClickedEventArgs? received = null;
        center.Clicked += args => received = args;

        backend.RaiseClicked("never-shown");

        Assert.NotNull(received);
        Assert.Equal("never-shown", received.Id);
        Assert.Null(received.Tag);
    }

    [Fact]
    public async Task Clicked_TagMapIsBounded_OldestEvicted()
    {
        var (center, backend, _) = Create();
        for (var i = 0; i <= NativeNotificationCenter.TagMapCapacity; i++)
            await center.ShowAsync(new NativeNotification { Id = $"n{i}", Title = "T", Tag = $"tag{i}" });
        var tags = new List<string?>();
        center.Clicked += args => tags.Add(args.Tag);

        backend.RaiseClicked("n0");
        backend.RaiseClicked("n1");
        backend.RaiseClicked($"n{NativeNotificationCenter.TagMapCapacity}");

        Assert.Equal(new string?[] { null, "tag1", $"tag{NativeNotificationCenter.TagMapCapacity}" }, tags);
    }

    [Fact]
    public async Task ShowAsync_Unsupported_CompletesWithoutBackendAndWarnsOnce()
    {
        var (center, backend, warnings) = Create(b =>
        {
            b.IsSupported = false;
            b.UnsupportedReason = "not bundled";
        });

        await center.ShowAsync(new NativeNotification { Title = "One" });
        await center.ShowAsync(new NativeNotification { Title = "Two" });

        Assert.Empty(backend.Shown);
        Assert.Equal(0, backend.PermissionRequests);
        var warning = Assert.Single(warnings);
        Assert.Contains("not bundled", warning);
    }

    [Fact]
    public async Task RequestPermissionAsync_Unsupported_ReturnsFalseWithoutBackend()
    {
        var (center, backend, _) = Create(b => b.IsSupported = false);

        var granted = await center.RequestPermissionAsync();

        Assert.False(granted);
        Assert.Equal(0, backend.PermissionRequests);
    }

    [Fact]
    public async Task ShowAsync_AutoRequestsPermissionOnce()
    {
        var (center, backend, _) = Create();

        await center.ShowAsync(new NativeNotification { Title = "One" });
        await center.ShowAsync(new NativeNotification { Title = "Two" });

        Assert.Equal(1, backend.PermissionRequests);
        Assert.Equal(new[] { "RequestPermission", "Show:" + backend.Shown[0].Id, "Show:" + backend.Shown[1].Id }, backend.Operations);
    }

    [Fact]
    public async Task ShowAsync_AfterExplicitPermission_DoesNotRequestAgain()
    {
        var (center, backend, _) = Create();

        await center.RequestPermissionAsync();
        await center.ShowAsync(new NativeNotification { Title = "One" });

        Assert.Equal(1, backend.PermissionRequests);
    }

    [Fact]
    public async Task RequestPermissionAsync_ExplicitCalls_AlwaysForward()
    {
        var (center, backend, _) = Create();

        await center.RequestPermissionAsync();
        await center.RequestPermissionAsync();

        Assert.Equal(2, backend.PermissionRequests);
    }

    [Fact]
    public async Task ShowAsync_PermissionDenied_SkipsBackendAndWarnsOnce()
    {
        var (center, backend, warnings) = Create(b => b.PermissionResult = false);

        await center.ShowAsync(new NativeNotification { Title = "One" });
        await center.ShowAsync(new NativeNotification { Title = "Two" });

        Assert.Empty(backend.Shown);
        Assert.Single(warnings);
    }

    [Fact]
    public async Task ShowAsync_BackendThrows_Propagates()
    {
        var (center, _, _) = Create(b => b.ShowException = new InvalidOperationException("daemon rejected"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => center.ShowAsync(new NativeNotification { Title = "T" }));

        Assert.Equal("daemon rejected", ex.Message);
    }

    [Fact]
    public void DismissAndDismissAll_Forward()
    {
        var (center, backend, _) = Create();

        center.Dismiss("n1");
        center.DismissAll();

        Assert.Equal(new[] { "n1" }, backend.Dismissed);
        Assert.Equal(1, backend.DismissAllCount);
    }

    [Fact]
    public async Task Dispose_DisposesBackend_AndMembersThrow()
    {
        var (center, backend, _) = Create();

        center.Dispose();

        Assert.True(backend.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => center.ShowAsync(new NativeNotification { Title = "T" }));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => center.RequestPermissionAsync());
        Assert.Throws<ObjectDisposedException>(() => center.Dismiss("n1"));
        Assert.Throws<ObjectDisposedException>(() => center.DismissAll());
    }

    [Fact]
    public void Dispose_Twice_IsSafe()
    {
        var (center, _, _) = Create();

        center.Dispose();
        center.Dispose();
    }

    [Fact]
    public void Clicked_AfterDispose_IsNotRaised()
    {
        var (center, backend, _) = Create();
        var raised = false;
        center.Clicked += _ => raised = true;
        center.Dispose();

        backend.RaiseClicked("n1");

        Assert.False(raised);
    }

    [Fact]
    public void IsSupported_AndReason_ForwardFromBackend()
    {
        var (center, _, _) = Create(b =>
        {
            b.IsSupported = false;
            b.UnsupportedReason = "why";
        });

        Assert.False(center.IsSupported);
        Assert.Equal("why", center.UnsupportedReason);
    }
}
