using System.Diagnostics;

namespace Hermes.Diagnostics;

/// <summary>
/// Simple logging abstraction for Hermes framework diagnostics.
/// </summary>
/// <remarks>
/// <para>Set the static delegate properties to integrate with your logging framework:</para>
/// <code>
/// HermesLogger.LogError = (msg, ex) => myLogger.Error(ex, msg);
/// HermesLogger.LogWarning = msg => myLogger.Warn(msg);
/// </code>
/// <para>In DEBUG builds, messages are also written to Console.Error if no handler is set.</para>
/// </remarks>
public static class HermesLogger
{
    /// <summary>
    /// Delegate for error logging. Called with message and optional exception.
    /// </summary>
    public static Action<string, Exception?>? LogError { get; set; }

    /// <summary>
    /// Delegate for warning logging.
    /// </summary>
    public static Action<string>? LogWarning { get; set; }

    /// <summary>
    /// Delegate for informational logging.
    /// </summary>
    public static Action<string>? LogInfo { get; set; }

    /// <summary>
    /// Delegate for debug/trace logging.
    /// </summary>
    public static Action<string>? LogDebug { get; set; }

    internal static void Error(string message, Exception? exception = null)
    {
        if (LogError is not null)
        {
            LogError(message, exception);
        }
        else
        {
            WriteDebugFallback("ERROR", message, exception);
        }
    }

    internal static void Warning(string message)
    {
        if (LogWarning is not null)
        {
            LogWarning(message);
        }
        else
        {
            WriteDebugFallback("WARN", message);
        }
    }

    internal static void Info(string message)
    {
        if (LogInfo is not null)
        {
            LogInfo(message);
        }
        else
        {
            WriteDebugFallback("INFO", message);
        }
    }

    [Conditional("DEBUG")]
    private static void WriteDebugFallback(string level, string message, Exception? exception = null)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        if (exception is not null)
        {
            Console.Error.WriteLine($"[Hermes {level}] {timestamp}: {message} - {exception.GetType().Name}: {exception.Message}");
        }
        else
        {
            Console.Error.WriteLine($"[Hermes {level}] {timestamp}: {message}");
        }
    }
}
