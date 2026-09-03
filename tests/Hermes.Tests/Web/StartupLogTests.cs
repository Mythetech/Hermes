// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Globalization;
using Hermes.Blazor.Diagnostics;
using Xunit;

namespace Hermes.Tests.Web;

// StartupLog is process-global static state. Other test classes in this suite
// exercise HermesWebViewManager through BuildForTest and Run(), and call
// StartupLog.Phase concurrently with these tests, which swap a plain List<string>
// in as the sink. xUnit runs a DisableParallelization collection only after every
// parallel collection has completed, so that ordering, not the test count, is what
// keeps the swapped sink private.
[CollectionDefinition(nameof(StartupLogTests), DisableParallelization = true)]
public sealed class StartupLogTestsCollection { }

[Collection(nameof(StartupLogTests))]
public class StartupLogTests
{
    [Fact]
    public void Phase_WritesNothing_WhenDisabled()
    {
        var lines = new List<string>();
        using (StartupLog.Override(enabled: false, lines.Add))
        {
            StartupLog.Phase("window-shown");
        }

        Assert.Empty(lines);
    }

    [Fact]
    public void Phase_WritesNameAndElapsedMilliseconds_WhenEnabled()
    {
        var lines = new List<string>();
        using (StartupLog.Override(enabled: true, lines.Add))
        {
            StartupLog.Phase("window-shown");
        }

        var line = Assert.Single(lines);
        var parts = line.Split(':');
        Assert.Equal("HERMES_PHASE", parts[0]);
        Assert.Equal("window-shown", parts[1]);
        Assert.True(double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms));
        Assert.True(ms >= 0);
    }

    [Fact]
    public void PhaseOnce_WritesOnlyOnFirstCall()
    {
        var lines = new List<string>();
        var flag = 0;
        using (StartupLog.Override(enabled: true, lines.Add))
        {
            StartupLog.PhaseOnce("first-request", ref flag);
            StartupLog.PhaseOnce("first-request", ref flag);
        }

        Assert.Single(lines);
    }

    [Fact]
    public void Override_RestoresPreviousConfiguration_OnDispose()
    {
        var before = StartupLog.IsEnabled;
        using (StartupLog.Override(enabled: !before, _ => { }))
        {
            Assert.Equal(!before, StartupLog.IsEnabled);
        }

        Assert.Equal(before, StartupLog.IsEnabled);
    }
}
