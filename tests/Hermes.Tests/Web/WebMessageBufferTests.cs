// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Startup;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Web;

public class WebMessageBufferTests
{
    [Fact]
    public void Drain_ReplaysMessagesInArrivalOrder()
    {
        var backend = new RecordingWindowBackend();
        using var buffer = new WebMessageBuffer(backend);
        backend.SimulateWebMessage("first");
        backend.SimulateWebMessage("second");

        var replayed = new List<string>();
        buffer.Drain(replayed.Add);

        Assert.Equal(new[] { "first", "second" }, replayed);
    }

    [Fact]
    public void Drain_DetachesBeforeReplaying_SoLaterMessagesAreNotBuffered()
    {
        var backend = new RecordingWindowBackend();
        using var buffer = new WebMessageBuffer(backend);
        backend.SimulateWebMessage("early");

        var replayed = new List<string>();
        buffer.Drain(replayed.Add);
        backend.SimulateWebMessage("late");

        Assert.Equal(new[] { "early" }, replayed);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Drain_ReplaysNothing_WhenNoMessagesArrived()
    {
        var backend = new RecordingWindowBackend();
        using var buffer = new WebMessageBuffer(backend);

        var replayed = new List<string>();
        buffer.Drain(replayed.Add);

        Assert.Empty(replayed);
    }

    [Fact]
    public void Dispose_StopsBuffering()
    {
        var backend = new RecordingWindowBackend();
        var buffer = new WebMessageBuffer(backend);

        buffer.Dispose();
        backend.SimulateWebMessage("after-dispose");

        Assert.Equal(0, buffer.Count);
    }
}
