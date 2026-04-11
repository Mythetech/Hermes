// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;

namespace Hermes.Diagnostics;

/// <summary>
/// Process-scoped diagnostics session state shared across crash interception and
/// error reporting (e.g. Mythetech.Platform SDK). Auto-initialized at type load,
/// so consuming apps never need to populate these values manually.
/// </summary>
public static class HermesSession
{
    /// <summary>
    /// Anonymous per-launch session identifier. Auto-initialized to a fresh GUID
    /// the first time this type is accessed. Host apps may override by assigning
    /// their own value (e.g. a real session id from auth) before any crash or
    /// error report is emitted.
    /// </summary>
    public static string AnonymousSessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// UTC timestamp of the current process's start, sourced from
    /// <see cref="Process.StartTime"/>. Use this for telemetry, status displays,
    /// and session-length reporting.
    /// </summary>
    public static DateTimeOffset StartTime { get; } =
        new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);

    /// <summary>
    /// Time elapsed since <see cref="StartTime"/>. Convenience for consumers that
    /// want to report or display how long the current process has been running.
    /// </summary>
    public static TimeSpan Uptime => DateTimeOffset.UtcNow - StartTime;
}
