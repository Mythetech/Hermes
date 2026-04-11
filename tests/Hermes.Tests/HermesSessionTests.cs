// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Diagnostics;
using Xunit;

namespace Hermes.Tests;

public sealed class HermesSessionTests
{
    [Fact]
    public void AnonymousSessionId_IsAutoInitializedToParseableGuid()
    {
        Assert.False(string.IsNullOrWhiteSpace(HermesSession.AnonymousSessionId));
        Assert.True(Guid.TryParse(HermesSession.AnonymousSessionId, out _));
    }

    [Fact]
    public void AnonymousSessionId_CanBeOverriddenByHost()
    {
        var original = HermesSession.AnonymousSessionId;
        try
        {
            HermesSession.AnonymousSessionId = "host-supplied-value";
            Assert.Equal("host-supplied-value", HermesSession.AnonymousSessionId);
        }
        finally
        {
            HermesSession.AnonymousSessionId = original;
        }
    }

    [Fact]
    public void StartTime_IsInThePastAndStableAcrossReads()
    {
        var first = HermesSession.StartTime;
        Assert.True(first <= DateTimeOffset.UtcNow);
        Thread.Sleep(5);
        var second = HermesSession.StartTime;
        Assert.Equal(first, second);
    }

    [Fact]
    public void Uptime_IsNonNegativeAndIncreasesOverTime()
    {
        var first = HermesSession.Uptime;
        Assert.True(first >= TimeSpan.Zero);
        Thread.Sleep(10);
        var second = HermesSession.Uptime;
        Assert.True(second > first);
    }
}
