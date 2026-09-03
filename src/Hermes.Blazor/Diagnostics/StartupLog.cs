// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Globalization;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Opt-in startup phase trace. When HERMES_STARTUP_TRACE=1 is set, each phase
/// writes one line to stdout in the form HERMES_PHASE:name:ms, where ms is
/// milliseconds since process start. Unset, every call is a single boolean check.
/// The trace exists so CI runners, whose timing local hardware cannot reproduce,
/// can report where startup time goes.
/// </summary>
internal static class StartupLog
{
    private static readonly object s_lock = new();
    private static bool s_enabled = Environment.GetEnvironmentVariable("HERMES_STARTUP_TRACE") == "1";
    private static Action<string> s_sink = Console.WriteLine;
    private static DateTime? s_processStart;

    public static bool IsEnabled => s_enabled;

    public static void Phase(string name)
    {
        if (!s_enabled) return;
        Write(name);
    }

    /// <summary>
    /// Logs the phase the first time it is called with the given flag; later calls
    /// are no-ops. The caller owns the flag so unrelated first-time phases do not
    /// share state.
    /// </summary>
    public static void PhaseOnce(string name, ref int flag)
    {
        if (!s_enabled) return;
        if (Interlocked.Exchange(ref flag, 1) != 0) return;
        Write(name);
    }

    private static void Write(string name)
    {
        double elapsedMs;
        lock (s_lock)
        {
            s_processStart ??= Process.GetCurrentProcess().StartTime;
            elapsedMs = (DateTime.Now - s_processStart.Value).TotalMilliseconds;
        }

        s_sink($"HERMES_PHASE:{name}:{elapsedMs.ToString("F1", CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Test hook: overrides the environment gate and the output sink until the
    /// returned object is disposed.
    /// </summary>
    internal static IDisposable Override(bool enabled, Action<string> sink)
    {
        var previousEnabled = s_enabled;
        var previousSink = s_sink;
        s_enabled = enabled;
        s_sink = sink;
        return new Restore(() =>
        {
            s_enabled = previousEnabled;
            s_sink = previousSink;
        });
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
