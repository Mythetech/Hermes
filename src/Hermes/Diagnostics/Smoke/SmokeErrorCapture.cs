// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Diagnostics.Smoke;

/// <summary>
/// Feeds process-wide failures into a smoke session: unhandled exceptions, unobserved tasks, exceptions
/// escaping UI-thread callbacks, and errors Hermes logs itself.
/// </summary>
internal sealed class SmokeErrorCapture : IDisposable
{
    private readonly SmokeSession _session;
    private int _disposed;

    private SmokeErrorCapture(SmokeSession session) => _session = session;

    internal static SmokeErrorCapture Attach(SmokeSession session)
    {
        var capture = new SmokeErrorCapture(session);
        AppDomain.CurrentDomain.UnhandledException += capture.OnUnhandledException;

        // HermesCrashInterceptor subscribes to this separately; each handler does its own job.
        TaskScheduler.UnobservedTaskException += capture.OnUnobservedTaskException;
        HermesApplication.DispatcherUnhandledException += capture.OnDispatcherUnhandledException;
        HermesLogger.ErrorObserved += capture.OnHermesError;
        return capture;
    }

    internal void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"A non-exception object was thrown: {e.ExceptionObject}");
        _session.RecordFatalError("unhandled", exception);
    }

    internal void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var exception = e.Exception.InnerExceptions.Count == 1 ? e.Exception.InnerExceptions[0] : e.Exception;
        _session.RecordError("unobserved-task", exception);
    }

    internal void OnDispatcherUnhandledException(Exception exception) =>
        _session.RecordError("dispatcher", exception);

    internal void OnHermesError(string message, Exception? exception)
    {
        if (exception is null)
            _session.RecordError("hermes", "HermesError", message, null);
        else
            _session.RecordError("hermes", exception.GetType().Name, $"{message}: {exception.Message}", exception.StackTrace);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        HermesApplication.DispatcherUnhandledException -= OnDispatcherUnhandledException;
        HermesLogger.ErrorObserved -= OnHermesError;
    }
}
