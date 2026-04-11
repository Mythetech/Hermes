// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;
using System.Threading;
using Hermes.Contracts.Diagnostics;

namespace Hermes.Diagnostics;

/// <summary>
/// Intercepts unhandled crashes and invokes <see cref="OnCrash"/> with structured context.
/// Call <see cref="Enable"/> early in application startup to install handlers.
/// </summary>
public static class HermesCrashInterceptor
{
    /// <summary>
    /// Application callback invoked when a crash is intercepted.
    /// </summary>
    public static Action<HermesCrashContext>? OnCrash { get; set; }

    /// <summary>
    /// Product name included in crash context. Set by the application at startup.
    /// </summary>
    public static string? ProductName { get; set; }

    /// <summary>
    /// Product version included in crash context. Set by the application at startup.
    /// </summary>
    public static string? ProductVersion { get; set; }

    private static int _enabled;

    /// <summary>
    /// Whether crash interception is currently enabled.
    /// </summary>
    public static bool IsEnabled => _enabled == 1;

    /// <summary>
    /// Install managed crash interception handlers.
    /// Idempotent: calling multiple times has no additional effect.
    /// </summary>
    public static void Enable()
    {
        if (Interlocked.CompareExchange(ref _enabled, 1, 0) != 0)
            return;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        HermesLogger.Info("Crash interception enabled.");
    }

    /// <summary>
    /// Remove all crash interception handlers.
    /// </summary>
    public static void Disable()
    {
        if (Interlocked.CompareExchange(ref _enabled, 0, 1) != 1)
            return;

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        HermesLogger.Info("Crash interception disabled.");
    }

    /// <summary>
    /// Build a <see cref="HermesCrashContext"/> from an exception.
    /// Exposed internally so platform backends can invoke it for WebView crashes.
    /// </summary>
    internal static HermesCrashContext BuildCrashContext(Exception exception, CrashSource source)
    {
        return new HermesCrashContext(
            Exception: BuildExceptionInfo(exception),
            Platform: BuildPlatformInfo(),
            CrashedAt: DateTimeOffset.UtcNow,
            Source: source,
            AnonymousSessionId: HermesSession.AnonymousSessionId);
    }

    /// <summary>
    /// Invoke the OnCrash callback with a pre-built context.
    /// Used by platform backends for WebView crash events.
    /// </summary>
    internal static void NotifyCrash(HermesCrashContext context)
    {
        try
        {
            OnCrash?.Invoke(context);
        }
        catch (Exception ex)
        {
            HermesLogger.Error("Exception in OnCrash handler.", ex);
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            var context = BuildCrashContext(ex, CrashSource.UnhandledException);
            NotifyCrash(context);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception.InnerExceptions.Count == 1
            ? e.Exception.InnerExceptions[0]
            : e.Exception;

        var context = BuildCrashContext(exception, CrashSource.UnobservedTask);
        NotifyCrash(context);
    }

    private static HermesExceptionInfo BuildExceptionInfo(Exception exception)
    {
        return new HermesExceptionInfo(
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            Message: exception.Message,
            StackTrace: ParseStackTrace(exception),
            InnerException: exception.InnerException is not null
                ? BuildExceptionInfo(exception.InnerException)
                : null);
    }

    private static IReadOnlyList<HermesStackFrame> ParseStackTrace(Exception exception)
    {
        var trace = new StackTrace(exception, fNeedFileInfo: true);
        var frames = trace.GetFrames();

        if (frames is null || frames.Length == 0)
            return Array.Empty<HermesStackFrame>();

        var result = new List<HermesStackFrame>(frames.Length);
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            result.Add(new HermesStackFrame(
                FileName: frame.GetFileName(),
                MethodName: method?.Name,
                TypeName: method?.DeclaringType?.FullName,
                LineNumber: frame.GetFileLineNumber() is > 0 and var line ? line : null,
                ColumnNumber: frame.GetFileColumnNumber() is > 0 and var col ? col : null));
        }

        return result;
    }

    private static HermesPlatformInfo BuildPlatformInfo()
    {
        var osInfo = OSInfo.Current;
        return new HermesPlatformInfo(
            ProductName: ProductName ?? "Unknown",
            ProductVersion: ProductVersion ?? "0.0.0",
            OperatingSystem: osInfo.Platform,
            OsVersion: osInfo.Version,
            Architecture: osInfo.Architecture,
            DotNetVersion: Environment.Version.ToString());
    }
}
