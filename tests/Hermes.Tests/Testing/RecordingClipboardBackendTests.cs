// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.Testing;
using Xunit;

namespace Hermes.Tests.Testing;

public sealed class RecordingClipboardBackendTests
{
    private static RecordingClipboardBackend WithContent() => new()
    {
        Content = new HermesDataPackage
        {
            Text = "t",
            Html = "<b>t</b>",
            Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-a"] = new byte[] { 1 } },
        },
    };

    [Fact]
    public void Read_ReturnsOnlyRequestedFormats()
    {
        var backend = WithContent();

        var result = backend.Read(HermesDataFormats.Text, []);

        Assert.Equal("t", result.Text);
        Assert.Null(result.Html);
        Assert.Empty(result.Custom);
    }

    [Fact]
    public void Read_ReturnsListedCustomTypesThatExist()
    {
        var backend = WithContent();

        var result = backend.Read(HermesDataFormats.None, ["application/x-a", "application/x-missing"]);

        Assert.Equal(new[] { "application/x-a" }, result.Custom.Keys);
    }

    [Fact]
    public void Write_ReplacesContent_AndClearEmptiesIt()
    {
        var backend = WithContent();

        backend.Write(new HermesDataPackage { Text = "new" });
        Assert.Equal("new", backend.Content.Text);
        Assert.Null(backend.Content.Html);

        backend.Clear();
        Assert.True(backend.Content.IsEmpty);
        Assert.Equal(new[] { "Write", "Clear" }, backend.Operations);
    }

    [Fact]
    public void Monitoring_TracksState_AndRaiseChangedInvokesSubscribers()
    {
        var backend = new RecordingClipboardBackend();
        var raised = 0;
        backend.Changed += () => raised++;

        backend.StartMonitoring();
        Assert.True(backend.IsMonitoring);
        backend.RaiseChanged();
        backend.StopMonitoring();

        Assert.False(backend.IsMonitoring);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void StartMonitoring_WithException_RecordsTheCallThenThrowsWithoutMonitoring()
    {
        var failure = new InvalidOperationException("start failed");
        var backend = new RecordingClipboardBackend { StartMonitoringException = failure };

        var thrown = Assert.Throws<InvalidOperationException>(backend.StartMonitoring);

        Assert.Same(failure, thrown);
        Assert.False(backend.IsMonitoring);
        Assert.Equal(new[] { "StartMonitoring" }, backend.Operations);
    }
}
