// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Xunit;

namespace Hermes.Tests.Smoke;

/// <summary>
/// These tests attach to process-wide events, so they must not overlap with other tests raising them.
/// </summary>
[CollectionDefinition(nameof(ProcessWideEventsCollection), DisableParallelization = true)]
public sealed class ProcessWideEventsCollection
{
}

[Collection(nameof(ProcessWideEventsCollection))]
public class SmokeErrorCaptureTests
{
    private readonly SmokeHarness _harness = new();

    [Fact]
    public void DispatcherExceptions_AreRecorded()
    {
        var session = _harness.CreateSession(exitWhenDone: false);

        using (SmokeErrorCapture.Attach(session))
            HermesApplication.RaiseDispatcherUnhandledException(new InvalidOperationException("ui boom"));

        Assert.Contains("HERMES_SMOKE_ERROR: dispatcher: InvalidOperationException: ui boom", _harness.Lines);
    }

    [Fact]
    public void HermesErrors_AreRecorded()
    {
        var session = _harness.CreateSession(exitWhenDone: false);

        using (SmokeErrorCapture.Attach(session))
            HermesLogger.Error("Hosted services failed to start");

        Assert.Contains("HERMES_SMOKE_ERROR: hermes: HermesError: Hosted services failed to start", _harness.Lines);
    }

    [Fact]
    public void UnobservedTaskExceptions_AreRecordedWithTheInnerException()
    {
        var session = _harness.CreateSession(exitWhenDone: false);
        using var capture = SmokeErrorCapture.Attach(session);

        capture.OnUnobservedTaskException(null, new UnobservedTaskExceptionEventArgs(new AggregateException(new TimeoutException("slow"))));

        Assert.Contains("HERMES_SMOKE_ERROR: unobserved-task: TimeoutException: slow", _harness.Lines);
    }

    [Fact]
    public void UnhandledExceptions_ProduceTheVerdictImmediately()
    {
        var session = _harness.CreateSession(exitWhenDone: false);
        using var capture = SmokeErrorCapture.Attach(session);

        capture.OnUnhandledException(null, new UnhandledExceptionEventArgs(new InvalidOperationException("fatal"), isTerminating: true));

        Assert.Contains("HERMES_SMOKE_ERROR: unhandled: InvalidOperationException: fatal", _harness.Lines);
        Assert.StartsWith("HERMES_SMOKE_RESULT: FAILED", _harness.Lines[^1]);
    }

    [Fact]
    public void AfterDispose_NothingIsRecorded()
    {
        var session = _harness.CreateSession(exitWhenDone: false);
        var capture = SmokeErrorCapture.Attach(session);

        capture.Dispose();
        HermesApplication.RaiseDispatcherUnhandledException(new InvalidOperationException("late"));
        HermesLogger.Error("late");

        Assert.DoesNotContain(_harness.Lines, line => line.Contains("late"));
    }
}
