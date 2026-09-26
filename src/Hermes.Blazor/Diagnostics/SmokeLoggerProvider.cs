// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics.Smoke;
using Microsoft.Extensions.Logging;

namespace Hermes.Blazor.Diagnostics;

/// <summary>
/// Turns every Error and Critical log entry during a smoke run into a run error. Many failures, such as
/// a startup hook throwing, only ever surface as an error log.
/// </summary>
internal sealed class SmokeLoggerProvider(SmokeSession session) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SmokeLogger(session, categoryName);

    public void Dispose()
    {
    }

    private sealed class SmokeLogger(SmokeSession session, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel is LogLevel.Error or LogLevel.Critical;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = $"{category}: {formatter(state, exception)}";
            if (exception is null)
                session.RecordError("log", logLevel.ToString(), message, null);
            else
                session.RecordError("log", exception.GetType().Name, $"{message}: {exception.Message}", exception.StackTrace);
        }
    }
}
