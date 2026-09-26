// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Globalization;
using Hermes.Contracts.Diagnostics;

namespace Hermes.Diagnostics.Smoke;

/// <summary>
/// One smoke run: records milestones, gates, checks and errors, then prints the verdict and ends the
/// process. Hermes.Blazor drives it; nothing here depends on Blazor, so other hosts can reuse it.
/// </summary>
internal sealed class SmokeSession
{
    internal const string WindowShownMilestone = "window-shown";
    internal const string FirstRenderMilestone = "first-render";
    internal const string ChecksPhase = "checks";

    // Long enough for a normal close to finish tearing down, short enough that a hung close still
    // exits well inside the CI timeout.
    internal static readonly TimeSpan CloseBackstop = TimeSpan.FromSeconds(5);

    private readonly SmokeTestSettings _settings;
    private readonly SmokeSessionContext _context;
    private readonly object _lock = new();
    private readonly List<SmokeMilestone> _milestones = new();
    private readonly List<SmokeCheckOutcome> _checks = new();
    private readonly List<SmokeError> _errors = new();
    private readonly List<string> _requiredGates = new();
    private readonly Dictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _budget = new();
    private IReadOnlyList<SmokeCheck> _plannedChecks = [];
    private string? _runningCheck;
    private long _runningCheckStarted;
    private long _startTimestamp;
    private ITimer? _budgetTimer;
    private Action? _requestClose;
    private int _finished;

    internal SmokeSession(SmokeTestSettings settings, SmokeSessionContext context)
    {
        _settings = settings;
        _context = context;
    }

    internal SmokeReport? Report { get; private set; }

    internal bool IsFinished => Volatile.Read(ref _finished) == 1;

    // Held so the timer is not collected before it fires.
    private ITimer? BackstopTimer { get; set; }

    internal void Start()
    {
        lock (_lock)
        {
            _startTimestamp = _context.Time.GetTimestamp();
            WriteLineLocked(SmokeOutput.Start(_context.App));
        }

        _budgetTimer = _context.Time.CreateTimer(_ => OnBudgetExpired(), null, _settings.Timeout, Timeout.InfiniteTimeSpan);
    }

    internal void AttachCloseHandler(Action requestClose) => _requestClose = requestClose;

    internal void MarkMilestone(string name)
    {
        lock (_lock)
        {
            if (IsFinished || HasMilestoneLocked(name))
                return;

            var elapsed = _context.Time.GetElapsedTime(_startTimestamp);
            var milestone = new SmokeMilestone(name, (long)elapsed.TotalMilliseconds);
            _milestones.Add(milestone);

            // HERMES_READY predates smoke mode; Hermes's existing smoke action still keys on it.
            if (name == FirstRenderMilestone)
                WriteLineLocked(SmokeOutput.Ready(elapsed.TotalMilliseconds));

            WriteLineLocked(SmokeOutput.Milestone(milestone));
        }
    }

    internal void RequireGate(string name)
    {
        lock (_lock)
        {
            if (!_requiredGates.Contains(name))
                _requiredGates.Add(name);

            GetGateLocked(name);
        }
    }

    internal void CompleteGate(string name)
    {
        TaskCompletionSource gate;
        lock (_lock)
            gate = GetGateLocked(name);

        gate.TrySetResult();
    }

    internal void FailGate(string name, string reason)
    {
        RecordError("gate", "GateFailed", $"{name}: {reason}", null);
        CompleteGate(name);
    }

    internal void RecordError(string source, Exception exception) =>
        RecordError(source, exception.GetType().Name, exception.Message, exception.StackTrace);

    internal void RecordError(string source, string type, string message, string? stackTrace)
    {
        lock (_lock)
        {
            if (IsFinished)
                return;

            var error = new SmokeError(source, type, message, stackTrace);
            _errors.Add(error);
            WriteLineLocked(SmokeOutput.Error(error));
        }
    }

    /// <summary>
    /// An unhandled exception is about to end the process, so the verdict has to be produced now.
    /// </summary>
    internal void RecordFatalError(string source, Exception exception)
    {
        RecordError(source, exception);
        Finish(timedOutWaitingFor: null);
    }

    /// <summary>
    /// Called once the first render is marked: waits for every required gate, runs the checks in order,
    /// then produces the verdict. Never throws.
    /// </summary>
    internal async Task RunToVerdictAsync(IReadOnlyList<SmokeCheck> checks)
    {
        var budget = _budget.Token;
        string[] gates;
        lock (_lock)
        {
            _plannedChecks = checks;
            gates = _requiredGates.ToArray();
        }

        try
        {
            foreach (var name in gates)
            {
                Task gate;
                lock (_lock)
                    gate = GetGateLocked(name).Task;

                await gate.WaitAsync(budget);
            }

            foreach (var check in checks)
            {
                budget.ThrowIfCancellationRequested();
                var outcome = await RunCheckAsync(check, budget);

                lock (_lock)
                {
                    if (IsFinished)
                        return;

                    _checks.Add(outcome);
                    WriteLineLocked(SmokeOutput.Check(outcome));
                }
            }

            Finish(timedOutWaitingFor: null);
        }
        catch (OperationCanceledException) when (budget.IsCancellationRequested)
        {
            // The budget ran out or the session already finished; the verdict is already printed.
        }
    }

    private async Task<SmokeCheckOutcome> RunCheckAsync(SmokeCheck check, CancellationToken budget)
    {
        var started = _context.Time.GetTimestamp();
        lock (_lock)
        {
            _runningCheck = check.Name;
            _runningCheckStarted = started;
        }

        try
        {
            CancellationTokenSource timeout;
            try
            {
                timeout = new CancellationTokenSource(check.Timeout, _context.Time);
            }
            catch (Exception ex)
            {
                // An out-of-range check.Timeout (for example TimeSpan.MaxValue) must fail only this
                // check; RunToVerdictAsync promises never to throw.
                return Outcome(check.Name, started, SmokeCheckStatus.Failed, $"{ex.GetType().Name}: {ex.Message}");
            }

            using (timeout)
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(budget, timeout.Token))
            {
                try
                {
                    // WaitAsync bounds checks that ignore their token, so one stuck check cannot stall the run.
                    await check.RunAsync(linked.Token).WaitAsync(check.Timeout, _context.Time, budget);
                    return Outcome(check.Name, started, SmokeCheckStatus.Passed, null);
                }
                catch (OperationCanceledException) when (budget.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is TimeoutException || (ex is OperationCanceledException && timeout.IsCancellationRequested))
                {
                    return Outcome(check.Name, started, SmokeCheckStatus.Failed, $"Timed out after {FormatSeconds(check.Timeout)}");
                }
                catch (Exception ex)
                {
                    return Outcome(check.Name, started, SmokeCheckStatus.Failed, $"{ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            lock (_lock)
                _runningCheck = null;
        }
    }

    private void OnBudgetExpired()
    {
        SmokeReport report;
        lock (_lock)
        {
            if (!TryClaimFinishedLocked())
                return;

            var waitingFor = WaitingForLocked();
            if (_runningCheck is { } running)
            {
                var outcome = Outcome(running, _runningCheckStarted, SmokeCheckStatus.Failed, "Stopped when the run budget ran out");
                _checks.Add(outcome);
                WriteLineLocked(SmokeOutput.Check(outcome));
                _runningCheck = null;
            }

            report = BuildReportLocked(waitingFor);
        }

        RunExitSequence(report);
    }

    private void Finish(string? timedOutWaitingFor)
    {
        SmokeReport report;
        lock (_lock)
        {
            if (!TryClaimFinishedLocked())
                return;

            report = BuildReportLocked(timedOutWaitingFor);
        }

        RunExitSequence(report);
    }

    /// <summary>
    /// Must be called with <see cref="_lock"/> held. Claims <see cref="_finished"/> as the very first
    /// step, before any recording (for example OnBudgetExpired's "Stopped" line) that could otherwise
    /// re-enter through a writer that synchronously completes the running check: the lock is reentrant
    /// per thread, so without the claim landing first, such a re-entry could still see itself as not
    /// finished and double-record.
    /// </summary>
    private bool TryClaimFinishedLocked()
    {
        if (_finished == 1)
            return false;

        Volatile.Write(ref _finished, 1);
        _budgetTimer?.Dispose();
        return true;
    }

    /// <summary>
    /// Must be called with <see cref="_lock"/> held, after <see cref="TryClaimFinishedLocked"/> has
    /// already claimed the finish, so nothing can be added to the checks or errors between deciding to
    /// finish and taking this snapshot.
    /// </summary>
    private SmokeReport BuildReportLocked(string? timedOutWaitingFor)
    {
        foreach (var planned in _plannedChecks)
        {
            if (!_checks.Exists(c => c.Name == planned.Name))
                _checks.Add(new SmokeCheckOutcome(planned.Name, SmokeCheckStatus.NotRun, 0, null));
        }

        var passed = timedOutWaitingFor is null
            && HasMilestoneLocked(WindowShownMilestone)
            && HasMilestoneLocked(FirstRenderMilestone)
            && _checks.TrueForAll(c => c.Status == SmokeCheckStatus.Passed)
            && _errors.Count == 0;

        var report = new SmokeReport(
            _context.App,
            passed,
            (long)_context.Time.GetElapsedTime(_startTimestamp).TotalMilliseconds,
            timedOutWaitingFor,
            _milestones.ToArray(),
            _checks.ToArray(),
            _errors.ToArray());
        Report = report;
        WriteLineLocked(SmokeOutput.Result(report));
        return report;
    }

    /// <summary>
    /// Runs once, after <see cref="FinishLocked"/> has produced the report. Order matters here: cancelling
    /// the budget runs every registration on a check's linked token inline and can throw or block, so it
    /// must never come before the result file, the exit code and the backstop are already in place.
    /// </summary>
    private void RunExitSequence(SmokeReport report)
    {
        WriteResultFile(report);

        if (!_settings.ExitWhenDone)
        {
            CancelBudgetSafely();
            return;
        }

        var exitCode = report.Passed ? 0 : 1;
        _context.SetExitCode(exitCode);

        // Closing the last window on macOS makes AppKit terminate with exit code 0, which would turn a
        // failed run into a pass for anything reading the exit code. Exit immediately in this case, and
        // when there is nothing to close, rather than risk a throwing or blocking cancel first.
        if ((_context.IsMacOS && !report.Passed) || _requestClose is null)
        {
            _context.HardExit(exitCode);
            return;
        }

        BackstopTimer = _context.Time.CreateTimer(_ => _context.HardExit(exitCode), null, CloseBackstop, Timeout.InfiniteTimeSpan);
        CancelBudgetSafely();

        try
        {
            _requestClose();
        }
        catch (Exception)
        {
            // The close handler belongs to the app; an escaping exception must not crash the process
            // with a nonzero code on what may be a passed run. Dispose the backstop first so a
            // non-terminal HardExit (as in tests) is not invoked a second time when it later fires.
            BackstopTimer?.Dispose();
            _context.HardExit(exitCode);
        }
    }

    /// <summary>
    /// Cancelling resumes any waiter blocked on the budget token (gates, checks). A registration on a
    /// check's own token can throw when that happens; those callbacks belong to app checks and must
    /// never block or crash the exit path.
    /// </summary>
    private void CancelBudgetSafely()
    {
        try
        {
            _budget.Cancel();
        }
        catch (AggregateException)
        {
        }
    }

    private void WriteResultFile(SmokeReport report)
    {
        if (_settings.ResultPath is not { } path)
            return;

        try
        {
            _context.WriteFile(path, SmokeOutput.ToJson(report));
        }
        catch (Exception ex)
        {
            // The stdout verdict is already out and CI falls back to it, so a bad path must not block the exit.
            WriteLine(SmokeOutput.Warning($"Could not write the smoke result file to {path}: {ex.Message}"));
        }
    }

    private string WaitingForLocked()
    {
        if (!HasMilestoneLocked(WindowShownMilestone))
            return WindowShownMilestone;
        if (!HasMilestoneLocked(FirstRenderMilestone))
            return FirstRenderMilestone;

        foreach (var gate in _requiredGates)
        {
            if (!_gates[gate].Task.IsCompleted)
                return gate;
        }

        return ChecksPhase;
    }

    private SmokeCheckOutcome Outcome(string name, long started, SmokeCheckStatus status, string? error) =>
        new(name, status, (long)_context.Time.GetElapsedTime(started).TotalMilliseconds, error);

    private static string FormatSeconds(TimeSpan duration) =>
        duration.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";

    private bool HasMilestoneLocked(string name) => _milestones.Exists(m => m.Name == name);

    private TaskCompletionSource GetGateLocked(string name)
    {
        if (!_gates.TryGetValue(name, out var gate))
        {
            gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gates[name] = gate;
        }

        return gate;
    }

    private void WriteLine(string line)
    {
        lock (_lock)
            WriteLineLocked(line);
    }

    private void WriteLineLocked(string line)
    {
        _context.Output.WriteLine(line);
        _context.Output.Flush();
    }
}
