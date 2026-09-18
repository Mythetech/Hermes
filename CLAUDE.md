# Hermes

Cross-platform .NET desktop framework (Blazor hybrid over native WebViews). Original Mythetech work, not a Photino fork.

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

macOS is stricter: closing the last window makes AppKit call `[NSApp terminate:]`, which hard-exits the process with code 0. `WaitForClose()` never returns, so nothing after `Run()` executes on a real macOS close (no `DisposeAsync`, no hosted-service `StopAsync`, no exit code). Do not put must-run logic after `Run()` and expect it to work on macOS; `ScenarioRunner` documents the workaround for test drivers. Known open framework gap as of 2026-08-25.

## Verification commands

- Unit suite: `dotnet test tests/Hermes.Tests` (also builds the benchmark apps for the guard tests)
- Integration scenarios: `HERMES_INTEGRATION_TEST=1 HERMES_INTEGRATION_TEST_EXIT=1 dotnet run --project samples/IntegrationTestApp` (expects 14/14 PASS, exit 0)
  - The documented count was 12 before the notifications scenario landed, but the runner already reported 13: custom-titlebar-rendered is started by the runner and only passed from TitlebarPage, which the automated run never visits, so it never appears in the totals.
- Benchmark smoke, same thing PR CI runs: build the three apps and the harness in Release, then `dotnet run --project benchmarks/Hermes.Benchmarks.Harness -c Release -- --iterations 1 --warmup 0` (exit code is the verdict)

Run the unit suite after every change. Run the integration scenarios before declaring framework changes done.

## Conventions

- Planning and design docs go in `.internal/`, not `docs/`.
- Tests: xUnit. In tests that call `app.Run()`, restore the `SynchronizationContext` afterward or later awaits post into the recording backend's queue, which nothing pumps (see the `RunApp` helper in `HostedServiceTests`).
