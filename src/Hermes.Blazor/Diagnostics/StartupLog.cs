using System.Diagnostics;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Simple static diagnostic logger for tracking startup timing.
/// All log messages include elapsed time from first log call.
/// </summary>
public static class StartupLog
{
    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private static bool _firstMessageLogged;

    /// <summary>
    /// Log a message with elapsed time prefix.
    /// </summary>
    public static void Log(string category, string message)
    {
        var elapsed = _stopwatch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"[{elapsed,8:F2}ms] [{category}] {message}");
    }

    /// <summary>
    /// Log first Blazor message (only logs once).
    /// </summary>
    public static void LogFirstMessage()
    {
        if (_firstMessageLogged) return;
        _firstMessageLogged = true;
        Log("WebView", "First Blazor message received (JS initialized)");
    }

    /// <summary>
    /// Reset first message flag (for testing).
    /// </summary>
    public static void Reset()
    {
        _firstMessageLogged = false;
    }
}
