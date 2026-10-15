// Copyright (c) Mythetech. Licensed under the MIT License.
#if LINUX
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.Platforms.Linux;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("linux")]
public sealed class LinuxWindowBackendDragDropTests
{
    [Fact]
    public void DropMessages_PassThrough_WhenTheWindowDidNotOptIn()
    {
        using var backend = new LinuxWindowBackend();
        var forwarded = new List<string>();
        var reported = false;
        backend.WebMessageReceived += forwarded.Add;
        backend.DropReported += _ => reported = true;

        backend.ReceiveWebMessage("""{"type":"hermes-drop","seq":1,"x":0,"y":0}""");

        Assert.Equal(new[] { """{"type":"hermes-drop","seq":1,"x":0,"y":0}""" }, forwarded);
        Assert.False(reported);
    }

    [Fact]
    public void DropMessages_NeverReachWebMessageReceived_AndCarryNoDataWithoutTheShim()
    {
        using var backend = new LinuxWindowBackend { DragDropEnabled = true };
        var forwarded = new List<string>();
        var reports = new List<HermesDropReport>();
        backend.WebMessageReceived += forwarded.Add;
        backend.DropReported += reports.Add;

        backend.ReceiveWebMessage("""{"type":"hermes-drop","seq":4,"x":1,"y":2}""");
        backend.ReceiveWebMessage("""{"type":"hermes-drop-reset"}""");
        backend.ReceiveWebMessage("""{"type":"hermes-drop-bogus","seq":"x"}""");
        backend.ReceiveWebMessage("""{"type":"pong"}""");

        Assert.Equal(new[] { """{"type":"pong"}""" }, forwarded);
        Assert.Collection(
            reports,
            drop =>
            {
                Assert.Equal(HermesDropReportKind.Drop, drop.Kind);
                Assert.Equal(4, drop.Sequence);
                Assert.Null(drop.Data);
            },
            reset => Assert.Equal(HermesDropReportKind.Reset, reset.Kind));
    }

    [Fact]
    public void ASubscriberThatThrows_DoesNotEscapeTheMessagePath()
    {
        using var backend = new LinuxWindowBackend { DragDropEnabled = true };
        backend.DropReported += _ => throw new InvalidOperationException("subscriber");

        var exception = Record.Exception(() => backend.ReceiveWebMessage("""{"type":"hermes-drop-reset"}"""));

        Assert.Null(exception);
    }
}
#endif
