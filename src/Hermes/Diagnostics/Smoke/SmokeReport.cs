// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Reflection;
using System.Runtime.InteropServices;

namespace Hermes.Diagnostics.Smoke;

internal enum SmokeCheckStatus
{
    Passed,
    Failed,
    NotRun,
}

internal sealed record SmokeAppInfo(string Name, string Version, string Platform, string Architecture)
{
    internal static SmokeAppInfo FromCurrentProcess()
    {
        var assembly = Assembly.GetEntryAssembly();
        var name = assembly?.GetName().Name ?? "App";
        var informational = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Source Link appends "+<commit>" to the informational version; the commit is noise in a log line.
        var version = informational?.Split('+')[0] ?? assembly?.GetName().Version?.ToString() ?? "0.0.0";
        var platform = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

        return new SmokeAppInfo(name, version, platform, architecture);
    }
}

internal sealed record SmokeMilestone(string Name, long ElapsedMs);

internal sealed record SmokeCheckOutcome(string Name, SmokeCheckStatus Status, long DurationMs, string? Error);

internal sealed record SmokeError(string Source, string Type, string Message, string? StackTrace);

internal sealed record SmokeReport(
    SmokeAppInfo App,
    bool Passed,
    long DurationMs,
    string? TimedOutWaitingFor,
    IReadOnlyList<SmokeMilestone> Milestones,
    IReadOnlyList<SmokeCheckOutcome> Checks,
    IReadOnlyList<SmokeError> Errors);

internal sealed record SmokeCheck(string Name, TimeSpan Timeout, Func<CancellationToken, Task> RunAsync);
