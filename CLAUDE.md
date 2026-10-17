# Hermes

Cross-platform .NET desktop framework (Blazor hybrid over native WebViews). Original Mythetech work, not a Photino fork.

The license terms for reusing this code are in @AGENTS.md.

## Desktop entry points: STA rules (Windows)

Windows requires the main thread to be STA for WebView2. This has regressed twice; treat it as load-bearing.

- Desktop app entry points must be a synchronous `[STAThread] static void Main`. Block on trailing async work with `GetAwaiter().GetResult()`. `samples/IntegrationTestApp/Program.cs` is the canonical pattern.
- `[STAThread]` on an `async Task Main` is silently ignored: the runtime reads the apartment from the compiler-generated entry point wrapper, which does not inherit the attribute, so the thread starts MTA and every Windows run fails at window creation while Linux and macOS pass.
- Top-level statements are equally broken: the synthesized entry point cannot carry the attribute at all. Never rewrite an explicit `[STAThread] Main` into top-level statements or an async Main.
- `tests/Hermes.Tests/BenchmarkAppEntryPointTests.cs` enforces this two ways: a source check that auto-discovers every `Program.cs` under `samples/` and `benchmarks/Hermes.Benchmarks.Apps/`, and an authoritative PE metadata check that reads the compiled entry point token of the benchmark apps. New desktop apps in those trees are covered automatically; wire new benchmark apps into the metadata theory and the test csproj build-order references.

## Benchmark apps are CI artifacts, not scratch code

`benchmarks/Hermes.Benchmarks.Apps/*` are booted by CI and by the public benchmark workflow. They back public performance claims.

- `Hermes.Tests` builds them via `ReferenceOutputAssembly=false` project references so the entry point guard tests always have fresh binaries. Keep that wiring intact.
- The `Smoke Boot Benchmark Apps` step at the end of the `integration-test` job in `integration-test.yml` boots each app on every PR (one warmup boot to absorb cold starts, one counted boot). If it fails, the app is broken, not the step.
- The benchmark harness (`Hermes.Benchmarks.Harness`) exits nonzero when any app has failed iterations or a missing binary. Never change it to report failures only in the summary; a green run with buried failures is how a 30/30 Windows failure once shipped.

## Teardown after the message loop exits

Once `WaitForClose()` returns, the message pump is dead. Any await in a dispose path whose completion arrives on a non-UI thread posts its continuation into a queue nothing drains (macOS dead main queue; Windows UiInvokeQueue drops items after dispose). Teardown code must either complete synchronously on the UI thread or use blocking joins on thread-pool tasks. See `HermesBlazorApp.StopHost` for the pattern and rationale.

App code is covered for it since 1.4.4, which fixed MudBlazor apps hanging in `DisposeAsync()` (a page service awaited thread-pool work). Two things hold it up, and both are load-bearing:

- `HermesBlazorApp` sets `HermesSynchronizationContext.LoopEnded` when the loop returns, and from then on `Post` runs work on the thread pool instead of queueing it natively.
- `DisposeAsync()` waits for startup and for the page's teardown with `FinishesWithinAsync`, which blocks once the loop has ended. The native window has to be destroyed on the UI thread (on macOS the WKWebView's dealloc waits for the main thread), so `DisposeAsync()` must not resume on the thread pool before `_window.Dispose()`. Never put a plain `await` of unfinished work ahead of it.

macOS needs the native layer to keep AppKit out of the way: `[NSApp terminate:]` hard-exits the process with code 0 from inside the run loop, so `WaitForClose()` would never return and nothing after `Run()` would execute (no `DisposeAsync`, no hosted-service `StopAsync`, no exit code). That was an open gap until 1.4.2. Now the window that `waitForClose` is running for stops the run loop when it closes, `applicationShouldTerminateAfterLastWindowClosed` returns NO, and the `HermesApplication` override of `terminate:` turns a quit request (Cmd+Q, the Dock, a quit Apple event, logout) into a close of that window. Never route these paths back into AppKit's `terminate:`. A quit request does not raise `Closing` or ask `CloseRequestedHandler`.

On every platform the loop ends when the window passed to `WaitForClose()` closes, not when the last window does. The integration run prints `HERMES_TEARDOWN: PASSED` after `Run()` returns and the app is disposed, and CI requires it on all three platforms; a missing marker means the process was ended from inside the message loop again, or that `DisposeAsync()` never got past `PageTeardownProbe`, a page service that finishes disposing on another thread.

## Verification commands

- Unit suite: `dotnet test tests/Hermes.Tests` (also builds the benchmark apps for the guard tests)
- Integration scenarios: `HERMES_INTEGRATION_TEST=1 HERMES_INTEGRATION_TEST_EXIT=1 dotnet run --project samples/IntegrationTestApp` (expects 24/24 PASS on macOS, 25/25 on Linux, and 26/26 on Windows, `HERMES_TEARDOWN: PASSED`, exit 0)
  - custom-titlebar-rendered is only reported from TitlebarPage, which the automated run never visits, so it is not counted in either total.
  - The Linux count needs an X11 session with xdotool installed, as in CI; under Wayland both hotkey scenarios go to the desktop portal.
- Smoke mode: `HERMES_SMOKE_TEST=1 dotnet run --project samples/SmokeTestApp` (expects `HERMES_SMOKE_RESULT: PASSED`, exit 0). With `SMOKE_SAMPLE_FAULT=check|render|gate|log|dialog` every run must end `FAILED`. Integration runs do not enable smoke mode.
- Benchmark smoke, same thing PR CI runs: build the three apps and the harness in Release, then `dotnet run --project benchmarks/Hermes.Benchmarks.Harness -c Release -- --iterations 1 --warmup 0` (exit code is the verdict)

Run the unit suite after every change. Run the integration scenarios before declaring framework changes done.

## Conventions

- Planning and design docs go in `.internal/`, not `docs/`.
- Tests: xUnit. In tests that call `app.Run()`, restore the `SynchronizationContext` afterward so the Hermes context does not stay on the test thread (see the `RunApp` helper in `HostedServiceTests`). The recording backend never pumps, so after `Run()` returns it models the dead loop; `TeardownAfterLoopTests` relies on that.
