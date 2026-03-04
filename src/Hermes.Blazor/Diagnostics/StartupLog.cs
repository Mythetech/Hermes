// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Placeholder for startup diagnostics. Methods are no-ops in release builds.
/// </summary>
internal static class StartupLog
{
    [Conditional("DEBUG")]
    public static void Log(string category, string message) { }

    [Conditional("DEBUG")]
    public static void LogFirstMessage() { }

    [Conditional("DEBUG")]
    public static void Reset() { }
}
