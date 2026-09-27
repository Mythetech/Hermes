// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics.Smoke;
using Xunit;

namespace Hermes.Tests.Smoke;

public class SmokeSessionTests
{
    private readonly SmokeHarness _harness = new();

    [Fact]
    public void Start_PrintsTheStartLine()
    {
        _harness.CreateSession();

        Assert.Equal("HERMES_SMOKE_START: SmokeApp 1.2.3 Linux x64", _harness.Lines[0]);
    }

    [Fact]
    public void MarkMilestone_PrintsOnce_AndFirstRenderAlsoPrintsReady()
    {
        var session = _harness.CreateSession();
        _harness.Time.Advance(TimeSpan.FromMilliseconds(250));

        session.MarkMilestone(SmokeSession.FirstRenderMilestone);
        session.MarkMilestone(SmokeSession.FirstRenderMilestone);

        Assert.Equal(
            new[] { "HERMES_SMOKE_START: SmokeApp 1.2.3 Linux x64", "HERMES_READY:250.00", "HERMES_SMOKE_MILESTONE: first-render 250ms" },
            _harness.Lines);
    }

    [Fact]
    public async Task PassingRun_PrintsPassed_SetsExitCodeZero_AndClosesTheWindow()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([SmokeHarness.Check("app/ok")]);

        Assert.Contains("HERMES_SMOKE_CHECK_PASS: app/ok 0ms", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (1 checks)", _harness.Lines[^1]);
        Assert.Equal(new[] { 0 }, _harness.ExitCodes);
        Assert.Equal(1, _harness.CloseRequests);
        Assert.Empty(_harness.HardExits);
    }

    [Fact]
    public async Task Backstop_ForcesTheExit_WhenClosingDoesNotEndTheProcess()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);
        await session.RunToVerdictAsync([]);

        _harness.Time.Advance(SmokeSession.CloseBackstop);

        Assert.Equal(new[] { 0 }, _harness.HardExits);
    }

    [Fact]
    public async Task FailingCheck_FailsTheRun()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([SmokeHarness.Check("app/broken", _ => throw new InvalidOperationException("boom"))]);

        Assert.Contains("HERMES_SMOKE_CHECK_FAIL: app/broken 0ms - InvalidOperationException: boom", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (1/1 checks failed, 0 errors)", _harness.Lines[^1]);
        Assert.Equal(new[] { 1 }, _harness.ExitCodes);
        Assert.Equal(1, _harness.CloseRequests);
    }

    [Fact]
    public async Task CheckThatIgnoresItsToken_TimesOut_AndTheRunContinues()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);
        var stuck = new TaskCompletionSource();

        var run = session.RunToVerdictAsync([
            SmokeHarness.Check("app/stuck", _ => stuck.Task, TimeSpan.FromSeconds(2)),
            SmokeHarness.Check("app/after"),
        ]);
        _harness.Time.Advance(TimeSpan.FromSeconds(2));
        await run;

        Assert.Contains("HERMES_SMOKE_CHECK_FAIL: app/stuck 2000ms - Timed out after 2s", _harness.Lines);
        Assert.Contains("HERMES_SMOKE_CHECK_PASS: app/after 0ms", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (1/2 checks failed, 0 errors)", _harness.Lines[^1]);
    }

    [Fact]
    public async Task CheckThatThrowsItsOwnTimeoutException_IsReportedAsThatException_NotAsATimeout()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([SmokeHarness.Check("app/pipe", _ => throw new TimeoutException("pipe connect"))]);

        Assert.Contains("HERMES_SMOKE_CHECK_FAIL: app/pipe 0ms - TimeoutException: pipe connect", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (1/1 checks failed, 0 errors)", _harness.Lines[^1]);
    }

    [Fact]
    public void BudgetExpiringBeforeFirstRender_FailsWithTheMissingMilestone()
    {
        var session = _harness.CreateSession(timeout: TimeSpan.FromSeconds(10));
        session.MarkMilestone(SmokeSession.WindowShownMilestone);

        _harness.Time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (0/0 checks failed, 0 errors, timed out waiting for first-render)", _harness.Lines[^1]);
        Assert.Equal(new[] { 1 }, _harness.ExitCodes);
        Assert.True(session.IsFinished);
    }

    [Fact]
    public async Task BudgetExpiringOnAGate_NamesTheGate_AndMarksChecksNotRun()
    {
        var session = _harness.CreateSession(timeout: TimeSpan.FromSeconds(10));
        session.RequireGate("app-ready");
        SmokeHarness.Boot(session);

        var run = session.RunToVerdictAsync([SmokeHarness.Check("app/ok")]);
        _harness.Time.Advance(TimeSpan.FromSeconds(10));
        await run;

        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (0/1 checks failed, 0 errors, timed out waiting for app-ready)", _harness.Lines[^1]);
        Assert.Equal(SmokeCheckStatus.NotRun, Assert.Single(session.Report!.Checks).Status);
    }

    [Fact]
    public async Task GateCompletedBeforeItIsRequired_DoesNotBlock()
    {
        var session = _harness.CreateSession();
        session.CompleteGate("app-ready");
        session.RequireGate("app-ready");
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines[^1]);
    }

    [Fact]
    public async Task CompletingAGateTwiceOrAnUnknownGate_IsHarmless()
    {
        var session = _harness.CreateSession();
        session.RequireGate("app-ready");
        session.CompleteGate("app-ready");
        session.CompleteGate("app-ready");
        session.CompleteGate("never-required");
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines[^1]);
    }

    [Fact]
    public async Task FailedGate_RecordsAnError_AndTheChecksStillRun()
    {
        var session = _harness.CreateSession();
        session.RequireGate("app-ready");
        SmokeHarness.Boot(session);

        var run = session.RunToVerdictAsync([SmokeHarness.Check("app/ok")]);
        session.FailGate("app-ready", "initialization threw");
        await run;

        Assert.Contains("HERMES_SMOKE_ERROR: gate: GateFailed: app-ready: initialization threw", _harness.Lines);
        Assert.Contains("HERMES_SMOKE_CHECK_PASS: app/ok 0ms", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (0/1 checks failed, 1 error)", _harness.Lines[^1]);
    }

    [Fact]
    public async Task ErrorsBeforeTheVerdict_FailTheRun_AndLaterErrorsAreIgnored()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);
        session.RecordError("log", "Error", "App: something broke", null);

        await session.RunToVerdictAsync([]);
        session.RecordError("log", "Error", "after the verdict", null);

        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (0/0 checks failed, 1 error)", _harness.Lines[^1]);
        Assert.Single(session.Report!.Errors);
    }

    [Fact]
    public async Task FailedRunOnMacOS_ExitsBeforeClosing()
    {
        var session = _harness.CreateSession(isMacOS: true);
        SmokeHarness.Boot(session);
        session.RecordError("log", "Error", "x", null);

        await session.RunToVerdictAsync([]);

        Assert.Equal(new[] { 1 }, _harness.HardExits);
        Assert.Equal(0, _harness.CloseRequests);
    }

    [Fact]
    public async Task PassedRunOnMacOS_ClosesNormally()
    {
        var session = _harness.CreateSession(isMacOS: true);
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Equal(1, _harness.CloseRequests);
        Assert.Empty(_harness.HardExits);
    }

    [Fact]
    public async Task ExitWhenDoneOff_PrintsTheVerdict_AndLeavesTheAppRunning()
    {
        var session = _harness.CreateSession(exitWhenDone: false);
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Equal("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines[^1]);
        Assert.Empty(_harness.ExitCodes);
        Assert.Empty(_harness.HardExits);
        Assert.Equal(0, _harness.CloseRequests);
    }

    [Fact]
    public async Task WithoutACloseHandler_ExitsImmediately()
    {
        var session = _harness.CreateSession(attachClose: false);
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Equal(new[] { 0 }, _harness.HardExits);
    }

    [Fact]
    public async Task ResultPath_WritesTheJsonReport()
    {
        var session = _harness.CreateSession(resultPath: "/tmp/smoke/result.json");
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Contains("\"result\": \"passed\"", _harness.Files["/tmp/smoke/result.json"]);
    }

    [Fact]
    public async Task UnwritableResultPath_PrintsAWarning_AndStillExits()
    {
        _harness.WriteFailure = new UnauthorizedAccessException("denied");
        var session = _harness.CreateSession(resultPath: "/root/result.json");
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Contains("HERMES_SMOKE_WARNING: Could not write the smoke result file to /root/result.json: denied", _harness.Lines);
        Assert.Equal(new[] { 0 }, _harness.ExitCodes);
        Assert.Equal(1, _harness.CloseRequests);
    }

    [Fact]
    public void FatalError_ProducesTheVerdictImmediately()
    {
        var session = _harness.CreateSession();
        session.MarkMilestone(SmokeSession.WindowShownMilestone);

        session.RecordFatalError("unhandled", new InvalidOperationException("crash"));

        Assert.Contains("HERMES_SMOKE_ERROR: unhandled: InvalidOperationException: crash", _harness.Lines);
        Assert.StartsWith("HERMES_SMOKE_RESULT: FAILED", _harness.Lines[^1]);
        Assert.True(session.IsFinished);
    }

    [Fact]
    public async Task BudgetExpiringWhileACheckIsRunning_RecordsTheCheckOnce_AndNamesTheChecksPhase()
    {
        var session = _harness.CreateSession(timeout: TimeSpan.FromSeconds(10));
        SmokeHarness.Boot(session);

        var run = session.RunToVerdictAsync([
            SmokeHarness.Check("app/slow", ct => Task.Delay(Timeout.InfiniteTimeSpan, ct), TimeSpan.FromSeconds(30)),
        ]);
        _harness.Time.Advance(TimeSpan.FromSeconds(10));
        await run;

        Assert.Contains("HERMES_SMOKE_CHECK_FAIL: app/slow 10000ms - Stopped when the run budget ran out", _harness.Lines);
        Assert.Equal("HERMES_SMOKE_RESULT: FAILED (1/1 checks failed, 0 errors, timed out waiting for checks)", _harness.Lines[^1]);
        Assert.Single(session.Report!.Checks);
    }

    [Fact]
    public async Task AdvancingTimePastTheBudget_AfterAPassingVerdict_ChangesNothing()
    {
        var session = _harness.CreateSession(timeout: TimeSpan.FromSeconds(10));
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);
        _harness.Time.Advance(TimeSpan.FromSeconds(10));

        Assert.Single(_harness.Lines, line => line.StartsWith("HERMES_SMOKE_RESULT:", StringComparison.Ordinal));
        Assert.Equal(new[] { 0 }, _harness.ExitCodes);
    }

    [Fact]
    public async Task CheckTokenRegistrationThatThrows_UnderAnExpiringBudget_StillCompletesTheExitSequence()
    {
        var session = _harness.CreateSession(timeout: TimeSpan.FromSeconds(10));
        SmokeHarness.Boot(session);
        var stuck = new TaskCompletionSource();
        var registrationRan = false;
        var queue = new QueueingSynchronizationContext();

        // Installed only long enough for RunToVerdictAsync's synchronous portion to run: everything down
        // to the check's first genuinely incomplete await happens on this thread before the call
        // returns, so every continuation in that chain captures this context rather than the test's own.
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(queue);
        var run = session.RunToVerdictAsync([
            SmokeHarness.Check("app/throws-on-cancel", ct =>
            {
                ct.Register(() =>
                {
                    registrationRan = true;
                    throw new InvalidOperationException("callback");
                });
                return stuck.Task;
            }, TimeSpan.FromSeconds(30)),
        ]);
        SynchronizationContext.SetSynchronizationContext(previousContext);

        // A separate, joined thread: Cancel() must run all of its own registrations to completion,
        // including the one that cancels the check's linked token, before the check's own completion
        // continuation (queued above rather than run inline) gets a chance to dispose that token first.
        var advancer = new Thread(() => _harness.Time.Advance(TimeSpan.FromSeconds(10)));
        advancer.Start();
        advancer.Join();

        Assert.True(registrationRan);
        Assert.Contains(_harness.Lines, line => line.StartsWith("HERMES_SMOKE_RESULT:", StringComparison.Ordinal));
        Assert.Equal(new[] { 1 }, _harness.ExitCodes);
        Assert.Equal(1, _harness.CloseRequests);

        // Leaves nothing pending: the check's own queued continuation still needs to run to observe the
        // budget cancellation and let RunToVerdictAsync's Task complete.
        queue.PumpAll();
        await run;
    }

    [Fact]
    public async Task CloseHandlerThatThrows_HardExitsWithTheExitCode()
    {
        var session = _harness.CreateSession();
        session.AttachCloseHandler(() => throw new InvalidOperationException("close failed"));
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([]);

        Assert.Equal(new[] { 0 }, _harness.HardExits);
    }

    [Fact]
    public async Task CheckWithOutOfRangeTimeout_IsReportedAsFailed_AndRemainingChecksStillRun()
    {
        var session = _harness.CreateSession();
        SmokeHarness.Boot(session);

        await session.RunToVerdictAsync([
            SmokeHarness.Check("app/bad-timeout", timeout: TimeSpan.MaxValue),
            SmokeHarness.Check("app/after"),
        ]);

        var badTimeout = session.Report!.Checks.Single(c => c.Name == "app/bad-timeout");
        var after = session.Report!.Checks.Single(c => c.Name == "app/after");
        Assert.Equal(SmokeCheckStatus.Failed, badTimeout.Status);
        Assert.Equal(SmokeCheckStatus.Passed, after.Status);
    }
}
