// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Globalization;
using System.Text.Json;
using Hermes.Diagnostics.Smoke;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeOutputTests
{
    private static readonly SmokeAppInfo App = new("Horizon", "2.3.0", "macOS", "arm64");

    private static SmokeReport Report(
        bool passed,
        SmokeCheckOutcome[]? checks = null,
        SmokeError[]? errors = null,
        string? timedOutWaitingFor = null) =>
        new(App, passed, 1234, timedOutWaitingFor, [new SmokeMilestone("window-shown", 400)], checks ?? [], errors ?? []);

    [Fact]
    public void Start_NamesTheAppVersionPlatformAndArchitecture()
    {
        Assert.Equal("HERMES_SMOKE_START: Horizon 2.3.0 macOS arm64", SmokeOutput.Start(App));
    }

    [Fact]
    public void Milestone_PrintsNameAndElapsedMilliseconds()
    {
        Assert.Equal("HERMES_SMOKE_MILESTONE: first-render 890ms", SmokeOutput.Milestone(new SmokeMilestone("first-render", 890)));
    }

    [Fact]
    public void Ready_UsesTwoDecimalsWithAPointInEveryCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("HERMES_READY:890.12", SmokeOutput.Ready(890.123));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Check_PrintsPassAndFailLines()
    {
        Assert.Equal("HERMES_SMOKE_CHECK_PASS: framework/storage 3ms",
            SmokeOutput.Check(new SmokeCheckOutcome("framework/storage", SmokeCheckStatus.Passed, 3, null)));
        Assert.Equal("HERMES_SMOKE_CHECK_FAIL: horizon/workspace-store 10002ms - Timed out after 10s",
            SmokeOutput.Check(new SmokeCheckOutcome("horizon/workspace-store", SmokeCheckStatus.Failed, 10002, "Timed out after 10s")));
    }

    [Fact]
    public void Error_KeepsOnlyTheFirstLine_SoCiCanParseIt()
    {
        var line = SmokeOutput.Error(new SmokeError("blazor", "InvalidOperationException", "Theme palette was null\r\n   at App.Render()", null));

        Assert.Equal("HERMES_SMOKE_ERROR: blazor: InvalidOperationException: Theme palette was null", line);
    }

    [Fact]
    public void Result_Passed_CountsChecks()
    {
        var checks = new[]
        {
            new SmokeCheckOutcome("a", SmokeCheckStatus.Passed, 1, null),
            new SmokeCheckOutcome("b", SmokeCheckStatus.Passed, 1, null),
        };

        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (2 checks)", SmokeOutput.Result(Report(true, checks)));
    }

    [Fact]
    public void Result_Failed_CountsFailedChecksErrorsAndTheTimeout()
    {
        var checks = new[]
        {
            new SmokeCheckOutcome("a", SmokeCheckStatus.Failed, 1, "boom"),
            new SmokeCheckOutcome("b", SmokeCheckStatus.Passed, 1, null),
            new SmokeCheckOutcome("c", SmokeCheckStatus.NotRun, 0, null),
        };
        var errors = new[] { new SmokeError("log", "Error", "x", null) };

        Assert.Equal(
            "HERMES_SMOKE_RESULT: FAILED (1/3 checks failed, 1 error, timed out waiting for app-ready)",
            SmokeOutput.Result(Report(false, checks, errors, "app-ready")));
    }

    [Fact]
    public void Result_Failed_UsesPluralErrors()
    {
        var errors = new[] { new SmokeError("log", "Error", "x", null), new SmokeError("log", "Error", "y", null) };

        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (0/0 checks failed, 2 errors)", SmokeOutput.Result(Report(false, errors: errors)));
    }

    [Fact]
    public void ToJson_ContainsTheWholeReport()
    {
        var report = Report(
            false,
            checks: [new("framework/initialization", SmokeCheckStatus.Passed, 3, null), new("app/slow", SmokeCheckStatus.NotRun, 0, null)],
            errors: [new("blazor", "UnhandledException", "boom", "at X")],
            timedOutWaitingFor: "app-ready");

        using var json = JsonDocument.Parse(SmokeOutput.ToJson(report));
        var root = json.RootElement;

        Assert.Equal(1, root.GetProperty("schema").GetInt32());
        Assert.Equal("Horizon", root.GetProperty("app").GetString());
        Assert.Equal("2.3.0", root.GetProperty("version").GetString());
        Assert.Equal("macOS", root.GetProperty("platform").GetString());
        Assert.Equal("arm64", root.GetProperty("architecture").GetString());
        Assert.Equal("failed", root.GetProperty("result").GetString());
        Assert.Equal(1234, root.GetProperty("durationMs").GetInt64());
        Assert.Equal("app-ready", root.GetProperty("timedOutWaitingFor").GetString());
        Assert.Equal("window-shown", root.GetProperty("milestones")[0].GetProperty("name").GetString());
        Assert.Equal(400, root.GetProperty("milestones")[0].GetProperty("elapsedMs").GetInt64());
        Assert.Equal("passed", root.GetProperty("checks")[0].GetProperty("status").GetString());
        Assert.Equal("not-run", root.GetProperty("checks")[1].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("checks")[0].GetProperty("error").ValueKind);
        Assert.Equal("blazor", root.GetProperty("errors")[0].GetProperty("source").GetString());
        Assert.Equal("at X", root.GetProperty("errors")[0].GetProperty("stackTrace").GetString());
    }

    [Fact]
    public void ToJson_StaysValidForQuotesNewlinesAndNonAscii()
    {
        const string message = "Couldn't load \"Café\" settings\n  at Load()";
        var report = Report(false, errors: [new("log", "Error", message, null)]);

        using var json = JsonDocument.Parse(SmokeOutput.ToJson(report));

        Assert.Equal(message, json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("timedOutWaitingFor").ValueKind);
    }
}
