// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Testing;

public sealed class RecordingNotificationBackendTests
{
    [Fact]
    public async Task ShowAsync_RecordsOperationAndFields()
    {
        var backend = new RecordingNotificationBackend();

        await backend.ShowAsync("id1", "Title", "Body", "/tmp/icon.png", silent: true);

        Assert.Contains("Show:id1", backend.Operations);
        var shown = Assert.Single(backend.Shown);
        Assert.Equal(("id1", "Title", "Body", "/tmp/icon.png", true), shown);
    }

    [Fact]
    public async Task ShowAsync_WithConfiguredException_Throws()
    {
        var backend = new RecordingNotificationBackend { ShowException = new InvalidOperationException("boom") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => backend.ShowAsync("id1", "T", null, null, false));

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsConfiguredResultAndCounts()
    {
        var backend = new RecordingNotificationBackend { PermissionResult = false };

        var granted = await backend.RequestPermissionAsync();

        Assert.False(granted);
        Assert.Equal(1, backend.PermissionRequests);
    }

    [Fact]
    public void RaiseClicked_InvokesClickedWithId()
    {
        var backend = new RecordingNotificationBackend();
        string? received = null;
        backend.Clicked += id => received = id;

        backend.RaiseClicked("id9");

        Assert.Equal("id9", received);
    }

    [Fact]
    public void DismissAndDismissAll_AreRecorded()
    {
        var backend = new RecordingNotificationBackend();

        backend.Dismiss("id1");
        backend.DismissAll();

        Assert.Equal(new[] { "id1" }, backend.Dismissed);
        Assert.Equal(1, backend.DismissAllCount);
        Assert.Equal(new[] { "Dismiss:id1", "DismissAll" }, backend.Operations);
    }

    [Fact]
    public void Dispose_SetsIsDisposed()
    {
        var backend = new RecordingNotificationBackend();

        backend.Dispose();

        Assert.True(backend.IsDisposed);
    }
}
