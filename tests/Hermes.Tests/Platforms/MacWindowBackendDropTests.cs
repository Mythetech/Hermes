// Copyright (c) Mythetech. Licensed under the MIT License.
#if MACOS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.Platforms.macOS;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("macos")]
public sealed class MacWindowBackendDropTests
{
    [Theory]
    [InlineData("""{"type":"hermes-drop","seq":1,"x":2,"y":3}""")]
    [InlineData("""{"type":"hermes-drop-reset"}""")]
    [InlineData("""{"type":"hermes-drag","action":"drag"}""")]
    [InlineData("__bwv:[]")]
    public void WithoutDragDrop_EveryMessageIsAWebMessage(string message)
    {
        using var backend = new MacWindowBackend();
        var forwarded = new List<string>();
        var reported = false;
        backend.WebMessageReceived += forwarded.Add;
        backend.DropReported += _ => reported = true;

        backend.HandleWebMessage(message);

        Assert.Equal(new[] { message }, forwarded);
        Assert.False(reported);
    }

    [Fact]
    public void ToDropReport_GivesADropTheCapture()
    {
        var capture = new HermesDataPackage { Files = ["/tmp/a.txt"] };

        var report = MacWindowBackend.ToDropReport("""{"type":"hermes-drop","seq":4,"x":12.5,"y":40}""", () => capture);

        Assert.NotNull(report);
        Assert.Equal(HermesDropReportKind.Drop, report.Kind);
        Assert.Equal(4, report.Sequence);
        Assert.Equal(12.5, report.X);
        Assert.Equal(40, report.Y);
        Assert.Same(capture, report.Data);
    }

    [Fact]
    public void ToDropReport_ResetTakesNoCapture()
    {
        var taken = 0;

        var report = MacWindowBackend.ToDropReport("""{"type":"hermes-drop-reset"}""", () => { taken++; return null; });

        Assert.Equal(HermesDropReportKind.Reset, report?.Kind);
        Assert.Null(report?.Data);
        Assert.Equal(0, taken);
    }

    [Theory]
    [InlineData("""{"type":"hermes-drop","seq":"1"}""")]
    [InlineData("""{"type":"hermes-drop-anything"}""")]
    [InlineData("""{"type":"hermes-drop",""")]
    public void ToDropReport_MalformedMessages_GiveNothing_AndTakeNoCapture(string message)
    {
        var taken = 0;

        Assert.Null(MacWindowBackend.ToDropReport(message, () => { taken++; return null; }));
        Assert.Equal(0, taken);
    }

    // HermesTypes.h asserts the same offsets and size when the native library compiles.
    [Fact]
    public void WindowParams_MatchTheNativeLayout()
    {
        Assert.Equal(75, (int)Marshal.OffsetOf<HermesWindowParams>(nameof(HermesWindowParams.DragDrop)));
        Assert.Equal(80, (int)Marshal.OffsetOf<HermesWindowParams>(nameof(HermesWindowParams.OnClosing)));
        Assert.Equal(272, (int)Marshal.OffsetOf<HermesWindowParams>(nameof(HermesWindowParams.DragDropScript)));
        Assert.Equal(288, Marshal.SizeOf<HermesWindowParams>());
    }
}
#endif
