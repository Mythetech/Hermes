// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Diagnostics.Smoke;

/// <summary>
/// The HERMES_SMOKE_* stdout lines and the result JSON. CI matches these lines by prefix, so every
/// line is a single line and the formats are part of the smoke contract.
/// </summary>
internal static class SmokeOutput
{
    internal static string Start(SmokeAppInfo app) =>
        $"HERMES_SMOKE_START: {app.Name} {app.Version} {app.Platform} {app.Architecture}";

    internal static string Milestone(SmokeMilestone milestone) =>
        $"HERMES_SMOKE_MILESTONE: {milestone.Name} {milestone.ElapsedMs}ms";

    internal static string Ready(double elapsedMs) =>
        "HERMES_READY:" + elapsedMs.ToString("F2", CultureInfo.InvariantCulture);

    internal static string Check(SmokeCheckOutcome outcome) => outcome.Status == SmokeCheckStatus.Passed
        ? $"HERMES_SMOKE_CHECK_PASS: {outcome.Name} {outcome.DurationMs}ms"
        : $"HERMES_SMOKE_CHECK_FAIL: {outcome.Name} {outcome.DurationMs}ms - {SingleLine(outcome.Error ?? "Not run")}";

    internal static string Error(SmokeError error) =>
        $"HERMES_SMOKE_ERROR: {error.Source}: {error.Type}: {SingleLine(error.Message)}";

    internal static string Warning(string message) =>
        $"HERMES_SMOKE_WARNING: {SingleLine(message)}";

    internal static string Result(SmokeReport report)
    {
        var total = report.Checks.Count;
        if (report.Passed)
            return $"HERMES_SMOKE_RESULT: PASSED ({total} checks)";

        var failed = report.Checks.Count(c => c.Status == SmokeCheckStatus.Failed);
        var errors = report.Errors.Count == 1 ? "1 error" : $"{report.Errors.Count} errors";
        var timeout = report.TimedOutWaitingFor is null ? string.Empty : $", timed out waiting for {report.TimedOutWaitingFor}";
        return $"HERMES_SMOKE_RESULT: FAILED ({failed}/{total} checks failed, {errors}{timeout})";
    }

    internal static string ToJson(SmokeReport report)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteString("app", report.App.Name);
            writer.WriteString("version", report.App.Version);
            writer.WriteString("platform", report.App.Platform);
            writer.WriteString("architecture", report.App.Architecture);
            writer.WriteString("result", report.Passed ? "passed" : "failed");
            writer.WriteNumber("durationMs", report.DurationMs);
            writer.WriteString("timedOutWaitingFor", report.TimedOutWaitingFor);

            writer.WriteStartArray("milestones");
            foreach (var milestone in report.Milestones)
            {
                writer.WriteStartObject();
                writer.WriteString("name", milestone.Name);
                writer.WriteNumber("elapsedMs", milestone.ElapsedMs);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("checks");
            foreach (var check in report.Checks)
            {
                writer.WriteStartObject();
                writer.WriteString("name", check.Name);
                writer.WriteString("status", StatusName(check.Status));
                writer.WriteNumber("durationMs", check.DurationMs);
                writer.WriteString("error", check.Error);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteStartArray("errors");
            foreach (var error in report.Errors)
            {
                writer.WriteStartObject();
                writer.WriteString("source", error.Source);
                writer.WriteString("type", error.Type);
                writer.WriteString("message", error.Message);
                writer.WriteString("stackTrace", error.StackTrace);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static string StatusName(SmokeCheckStatus status) => status switch
    {
        SmokeCheckStatus.Passed => "passed",
        SmokeCheckStatus.Failed => "failed",
        _ => "not-run",
    };

    private static string SingleLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }
}
