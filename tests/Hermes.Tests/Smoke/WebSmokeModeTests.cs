// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Collections.Concurrent;
using System.Diagnostics;
using Hermes.Abstractions;
using Hermes.Diagnostics;
using Hermes.Diagnostics.Smoke;
using Hermes.Testing;
using Hermes.Tests.Web;
using Hermes.Web;
using Hermes.Web.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Hermes.Tests.Smoke;

public sealed class WebSmokeModeTests
{
    private const string Ready = """{"type":"ready"}""";
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);
    private readonly SmokeHarness _harness = new();

    [Fact]
    public async Task PageReady_MarksFirstRender_RunsChecks_AndPasses()
    {
        var (app, backend) = Build(b => b.Services.AddSingleton(new CheckLog()).AddHermesSmokeCheck<LoggingCheck>(), exitWhenDone: true);

        WebAppRunner.Run(app);
        backend.SimulateWebMessage(Ready);
        await VerdictAsync(backend);
        await PumpUntilAsync(backend, () => backend.Recording.MethodWasCalled("Close"));

        Assert.Contains(_harness.Lines, l => l.StartsWith("HERMES_SMOKE_MILESTONE: window-shown", StringComparison.Ordinal));
        Assert.Contains(_harness.Lines, l => l.StartsWith("HERMES_SMOKE_MILESTONE: first-render", StringComparison.Ordinal));
        Assert.Equal(1, app.Services.GetRequiredService<CheckLog>().Runs);
        Assert.Contains("HERMES_SMOKE_RESULT: PASSED (1 checks)", _harness.Lines);
        Assert.Equal([0], _harness.ExitCodes);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task PageError_FailsTheRun_WithAPageError()
    {
        var (app, backend) = Build();

        WebAppRunner.Run(app);
        backend.SimulateWebMessage("""{"type":"page-error","source":"error","name":"TypeError","message":"boom","stack":null}""");
        backend.SimulateWebMessage(Ready);
        await VerdictAsync(backend);

        Assert.Contains("HERMES_SMOKE_ERROR: page: TypeError: boom", _harness.Lines);
        Assert.Contains(_harness.Lines, l => l.StartsWith("HERMES_SMOKE_RESULT: FAILED", StringComparison.Ordinal));
        await app.DisposeAsync();
    }

    [Fact]
    public async Task SecondReady_RunsTheChecksOnce()
    {
        var (app, backend) = Build(b => b.Services.AddSingleton(new CheckLog()).AddHermesSmokeCheck<LoggingCheck>());
        app.Bridge.Handle("ping", () => "pong");

        WebAppRunner.Run(app);
        backend.SimulateWebMessage(Ready);
        backend.SimulateWebMessage(Ready);
        backend.SimulateWebMessage("""{"type":"invoke","id":"1","method":"ping","args":[]}""");
        await PumpUntilAsync(backend, () => backend.Recording.SentWebMessageMatching("pong"));
        await VerdictAsync(backend);

        Assert.Equal(1, app.Services.GetRequiredService<CheckLog>().Runs);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Checks_CanUseTheBridgeWhileTheyRun()
    {
        var (app, backend) = Build(b => b.Services.AddHermesSmokeCheck<EchoCheck>());

        WebAppRunner.Run(app);
        backend.SimulateWebMessage(Ready);
        await PumpUntilAsync(backend, () => backend.Recording.SentWebMessageMatching("\"echo\""));
        backend.SimulateWebMessage("""{"type":"event","name":"echo.reply","data":7}""");
        await VerdictAsync(backend);

        Assert.Contains("HERMES_SMOKE_RESULT: PASSED (1 checks)", _harness.Lines);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Gates_AreRequired()
    {
        var (app, backend) = Build(b => b.Services.AddHermesSmokeGate("g"));

        WebAppRunner.Run(app);
        backend.SimulateWebMessage(Ready);
        await PumpUntilAsync(backend, () => _harness.Lines.Any(l => l.StartsWith("HERMES_SMOKE_MILESTONE: first-render", StringComparison.Ordinal)));
        await Task.Delay(100);
        Assert.DoesNotContain(_harness.Lines, l => l.StartsWith("HERMES_SMOKE_RESULT", StringComparison.Ordinal));

        app.Services.GetRequiredService<IHermesSmokeSession>().CompleteGate("g");
        await VerdictAsync(backend);

        Assert.Contains("HERMES_SMOKE_RESULT: PASSED (0 checks)", _harness.Lines);
        await app.DisposeAsync();
    }

    [Fact]
    public async Task Dialogs_AreSuppressed()
    {
        var (app, backend) = Build(b => b.Services.AddHermesSmokeCheck<DialogCheck>());

        WebAppRunner.Run(app);
        backend.SimulateWebMessage(Ready);
        await VerdictAsync(backend);

        Assert.Contains(_harness.Lines, l => l.StartsWith("HERMES_SMOKE_ERROR: dialog:", StringComparison.Ordinal));
        await app.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_DuringAHungCheck_DoesNotHang()
    {
        var (app, backend) = Build(b =>
        {
            b.Services.AddSingleton(new CheckLog()).AddHermesSmokeCheck<HungCheck>();
            b.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromMilliseconds(200));
        });

        WebAppRunner.Run(app);
        backend.SimulateWebMessage(Ready);
        var log = app.Services.GetRequiredService<CheckLog>();
        await PumpUntilAsync(backend, () => log.Runs == 1);

        var stopwatch = Stopwatch.StartNew();
        await app.DisposeAsync().AsTask().WaitAsync(Wait);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"DisposeAsync took {stopwatch.Elapsed}.");
    }

    private (HermesWebApp App, RecordingWindowBackend Backend) Build(Action<HermesWebAppBuilder>? configure = null, bool exitWhenDone = false)
    {
        var builder = HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Production"]);
        builder.UseSmokeSessionForTest(_harness.CreateSession(resultPath: "result.json", exitWhenDone: exitWhenDone));
        configure?.Invoke(builder);
        var backend = new RecordingWindowBackend();
        return (HermesWebAppBuilder.BuildForTest(builder, backend), backend);
    }

    /// <summary>Waits for the verdict, running the work the app posted to the UI thread meanwhile.</summary>
    private async Task VerdictAsync(RecordingWindowBackend backend)
    {
        await PumpUntilAsync(backend, () => _harness.ResultFileWritten.Task.IsCompleted);
        backend.ProcessPending();
    }

    internal static async Task PumpUntilAsync(RecordingWindowBackend backend, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Wait;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the smoke run.");
            backend.ProcessPending();
            await Task.Delay(5);
        }
    }

    private sealed class CheckLog
    {
        private int _runs;

        public int Runs => Volatile.Read(ref _runs);

        public void Ran() => Interlocked.Increment(ref _runs);
    }

    private sealed class LoggingCheck(CheckLog log) : IHermesSmokeCheck
    {
        public string Name => "web/logging";

        public Task RunAsync(CancellationToken cancellationToken)
        {
            log.Ran();
            return Task.CompletedTask;
        }
    }

    private sealed class EchoCheck(InteropBridge bridge) : IHermesSmokeCheck
    {
        public string Name => "web/echo";

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var reply = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = bridge.On<int>("echo.reply", value => reply.TrySetResult(value));
            bridge.Send("echo", 7);
            Assert.Equal(7, await reply.Task.WaitAsync(cancellationToken));
        }
    }

    private sealed class DialogCheck(HermesWindow window) : IHermesSmokeCheck
    {
        public string Name => "web/dialog";

        public Task RunAsync(CancellationToken cancellationToken)
        {
            window.Dialogs.ShowMessage("title", "message", DialogButtons.Ok, DialogIcon.Info);
            return Task.CompletedTask;
        }
    }

    private sealed class HungCheck(CheckLog log) : IHermesSmokeCheck
    {
        public string Name => "web/hung";

        public TimeSpan Timeout => TimeSpan.FromSeconds(30);

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            log.Ran();
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        }
    }
}

/// <summary>Watches HermesLogger's process-wide error event, so it runs alone.</summary>
[Collection(nameof(ProcessWideEventsCollection))]
public sealed class WebPageErrorLoggingTests
{
    [Fact]
    public async Task PageError_OutsideSmokeMode_IsLoggedAsAnError()
    {
        var errors = new ConcurrentQueue<string>();
        Action<string, Exception?> sink = (message, _) => errors.Enqueue(message);
        HermesLogger.ErrorObserved += sink;
        try
        {
            var backend = new RecordingWindowBackend();
            var app = HermesWebAppBuilder.BuildForTest(HermesWebAppBuilder.CreateSlimBuilder(["--environment", "Production"]), backend);
            WebAppRunner.Run(app);

            backend.SimulateWebMessage("""{"type":"page-error","source":"unhandledrejection","name":"TypeError","message":"boom"}""");
            await WebSmokeModeTests.PumpUntilAsync(backend, () => errors.Any(e => e.Contains("boom", StringComparison.Ordinal)));

            await app.DisposeAsync();
        }
        finally
        {
            HermesLogger.ErrorObserved -= sink;
        }
    }
}
