// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Blazor.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeLoggerProviderTests
{
    private readonly SmokeHarness _harness = new();

    private ILogger CreateLogger(string category)
    {
        var provider = new SmokeLoggerProvider(_harness.CreateSession(exitWhenDone: false));
        return provider.CreateLogger(category);
    }

    [Fact]
    public void Errors_AreRecorded_WithTheCategoryAndMessage()
    {
        var logger = CreateLogger("App.Pages.Index");

        logger.LogWarning("Only a warning");
        logger.LogError("Could not load {Item}", "settings");

        Assert.Contains("HERMES_SMOKE_ERROR: log: Error: App.Pages.Index: Could not load settings", _harness.Lines);
        Assert.DoesNotContain(_harness.Lines, line => line.Contains("Only a warning"));
    }

    [Fact]
    public void ExceptionsUseTheExceptionType()
    {
        var logger = CreateLogger("App");

        logger.LogCritical(new InvalidOperationException("boom"), "Load failed");

        Assert.Contains("HERMES_SMOKE_ERROR: log: InvalidOperationException: App: Load failed: boom", _harness.Lines);
    }

    [Theory]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, false)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.None, false)]
    public void IsEnabled_OnlyForErrorAndCritical(LogLevel level, bool expected)
    {
        Assert.Equal(expected, CreateLogger("App").IsEnabled(level));
    }
}
