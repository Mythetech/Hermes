// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes;
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Contracts.Diagnostics;
using Hermes.Diagnostics;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// Runs integration test scenarios and reports results.
/// Scenarios test various Hermes features to ensure they work correctly.
/// </summary>
public sealed class ScenarioRunner : IDisposable
{
    private readonly HermesBlazorApp _app;
    private readonly VirtualizeProbe _virtualizeProbe;
    private readonly bool _autoExit;
    private bool _disposed;
    private volatile string? _latestColorScheme;

    public ScenarioRunner(HermesBlazorApp app, VirtualizeProbe virtualizeProbe, bool autoExit = false)
    {
        _app = app;
        _virtualizeProbe = virtualizeProbe;
        _autoExit = autoExit;
    }

    /// <summary>
    /// Run all test scenarios.
    /// Call this after the app is initialized and shown.
    /// </summary>
    public async Task RunAllScenariosAsync()
    {
        Console.WriteLine();
        Console.WriteLine("Starting integration test scenarios...");
        Console.WriteLine();

        // Window lifecycle tests
        RunWindowInitializationTest();
        RunWindowTitleTest();
        RunWindowSizeTest();

        // Menu tests
        RunMenuCreationTest();
        RunMenuAcceleratorTest();

        // Custom titlebar tests (will be validated by component)
        TestReporter.Start("custom-titlebar-rendered");

        // Platform visibility tests - these verify real backend behavior
        // Run resize/move/focus FIRST - they don't need WebView to be ready
        await RunResizeEventTestAsync();
        await RunMoveEventTestAsync();
        await RunFocusEventTestAsync();

        // Diagnostics session auto-init (must run before anything that overrides AnonymousSessionId)
        RunHermesSessionAutoInitTest();

        // Crash interception test
        await RunCrashInterceptionTestAsync();

        // Web message test runs LAST - gives WebView2 maximum time to initialize
        // On Windows, WebView2 init is async and can take several seconds in CI
        await RunWebMessageRoundTripTestAsync();

        // Virtualize interop runs after the web message round trip because both need the WebView
        // to be up; by this point the home page has long since rendered, so the probe is normally
        // already resolved and this returns immediately.
        await RunVirtualizeInteropTestAsync();

        // Theme reads prefers-color-scheme back from the page, so it also needs the WebView up
        await RunWindowThemeTestAsync();

        // Print summary
        TestReporter.PrintSummary();

        // Auto-exit if in CI mode
        if (_autoExit)
        {
            Console.WriteLine("Auto-exiting after test completion...");

            // The clean-close path exits through Main returning normally, so
            // the verdict must ride Environment.ExitCode; without this a
            // failed run exits 0 and only the force-exit backstop below ever
            // carried the failure.
            var exitCode = TestReporter.AllPassed ? 0 : 1;
            Environment.ExitCode = exitCode;

            // On macOS, closing the last window makes AppKit call
            // [NSApp terminate:], which hard-exits the process with code 0
            // before Main's epilogue runs, masking any failure. A failing run
            // must therefore exit managed before initiating the native close.
            if (OperatingSystem.IsMacOS() && exitCode != 0)
            {
                Console.WriteLine("Exiting before close: macOS native terminate would mask the failure exit code.");
                Environment.Exit(exitCode);
            }

            // Close must happen on the UI thread
            _app.MainWindow.Invoke(() => _app.MainWindow.Close());

            // Backstop: if Close() doesn't terminate the app within 5 seconds, force exit.
            // On some platforms (e.g. macOS) Close may not fully stop the run loop.
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                Console.WriteLine("Force-exiting: Close() did not terminate the app within 5s.");
                Environment.Exit(exitCode);
            });
        }
    }

    private void RunWindowInitializationTest()
    {
        TestReporter.Start("window-initialization");
        try
        {
            var window = _app.MainWindow;
            TestReporter.Assert("window-initialization",
                window != null,
                "MainWindow is null");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-initialization", ex.Message);
        }
    }

    private void RunWindowTitleTest()
    {
        TestReporter.Start("window-title");
        try
        {
            var expectedTitle = "Hermes Integration Tests";
            var actualTitle = _app.MainWindow.Title;
            TestReporter.Assert("window-title",
                actualTitle == expectedTitle,
                $"Expected '{expectedTitle}', got '{actualTitle}'");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-title", ex.Message);
        }
    }

    private void RunWindowSizeTest()
    {
        TestReporter.Start("window-size");
        try
        {
            var (width, height) = _app.MainWindow.Size;
            TestReporter.Assert("window-size",
                width > 0 && height > 0,
                $"Invalid size: {width}x{height}");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-size", ex.Message);
        }
    }

    private void RunMenuCreationTest()
    {
        TestReporter.Start("menu-creation");
        try
        {
            var menuBar = _app.MainWindow.MenuBar;
            TestReporter.Assert("menu-creation",
                menuBar != null,
                "MenuBar is null");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("menu-creation", ex.Message);
        }
    }

    private void RunMenuAcceleratorTest()
    {
        TestReporter.Start("menu-accelerator");
        try
        {
            // Menu modifications must happen on the UI thread (especially on macOS)
            // Use Invoke to marshal to the main thread
            Exception? invokeError = null;
            _app.MainWindow.Invoke(() =>
            {
                try
                {
                    var menuBar = _app.MainWindow.MenuBar;
                    menuBar.AddMenu("Test", test =>
                    {
                        test.AddItem("Test Item", "test.item", item =>
                            item.WithAccelerator("Ctrl+Shift+T"));
                    });
                }
                catch (Exception ex)
                {
                    invokeError = ex;
                }
            });

            if (invokeError != null)
                throw invokeError;

            TestReporter.Pass("menu-accelerator");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("menu-accelerator", ex.Message);
        }
    }

    private async Task RunWebMessageRoundTripTestAsync()
    {
        TestReporter.Start("web-message-roundtrip");
        try
        {
            var pongTcs = new TaskCompletionSource<string>();

            // Listen for messages from JS
            void handler(string msg)
            {
                Console.WriteLine($"WEB_MESSAGE_RECEIVED: {msg}");

                if (msg.Contains("pong"))
                    pongTcs.TrySetResult(msg);
            }

            _app.MainWindow.OnWebMessage(handler);

            // Send ping periodically until we get pong or 30s overall timeout.
            // WebView2 on Windows can take 30+ seconds to initialize in CI,
            // and the page may not have fully loaded on first attempt.
            using var overallCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _ = Task.Run(async () =>
            {
                while (!overallCts.IsCancellationRequested && !pongTcs.Task.IsCompleted)
                {
                    try
                    {
                        Console.WriteLine("WEB_MESSAGE_TEST: Sending ping...");
                        _app.MainWindow.Invoke(() => _app.MainWindow.SendMessage("ping"));
                    }
                    catch
                    {
                        // SendMessage may fail if WebView not fully ready yet
                    }

                    try { await Task.Delay(2000, overallCts.Token); }
                    catch (OperationCanceledException) { break; }
                }
            }, overallCts.Token);

            var result = await pongTcs.Task.WaitAsync(overallCts.Token);

            TestReporter.Assert("web-message-roundtrip",
                result.Contains("pong"),
                $"Expected pong, got: {result}");
        }
        catch (OperationCanceledException)
        {
            TestReporter.Fail("web-message-roundtrip", "Timeout waiting for pong response after 30s");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("web-message-roundtrip", ex.Message);
        }
    }

    private async Task RunVirtualizeInteropTestAsync()
    {
        TestReporter.Start("blazor-virtualize-interop");
        try
        {
            // Same 30s budget as the web message round trip: WebView2 initialization on Windows CI
            // can take that long, and Virtualize cannot call into JS before it is ready.
            var completed = await _virtualizeProbe.WaitAsync(TimeSpan.FromSeconds(30));

            TestReporter.Assert("blazor-virtualize-interop",
                completed,
                _virtualizeProbe.Error);
        }
        catch (Exception ex)
        {
            TestReporter.Fail("blazor-virtualize-interop", ex.Message);
        }
    }

    private async Task RunWindowThemeTestAsync()
    {
        TestReporter.Start("window-theme");
        try
        {
            var window = _app.MainWindow;

            // Linux has no per-window theme support, so only verify that setting it is harmless there
            if (window.Platform == HermesPlatform.Linux)
            {
                window.Theme = HermesWindowTheme.Dark;
                window.Theme = HermesWindowTheme.System;
                TestReporter.Pass("window-theme");
                return;
            }

            window.OnWebMessage(msg =>
            {
                if (msg.Contains("\"color-scheme\""))
                    _latestColorScheme = msg.Contains("\"dark\"") ? "dark" : "light";
            });

            // Checking both directions keeps the scenario meaningful whatever the runner's OS
            // appearance is. The theme is set from this background thread on purpose, since the
            // setter must marshal to the UI thread itself.
            var lightScheme = await ApplyThemeAndWaitForColorSchemeAsync(HermesWindowTheme.Light, "light");
            var darkScheme = await ApplyThemeAndWaitForColorSchemeAsync(HermesWindowTheme.Dark, "dark");
            window.Theme = HermesWindowTheme.System;

            TestReporter.Assert("window-theme",
                lightScheme == "light" && darkScheme == "dark",
                $"prefers-color-scheme was '{lightScheme}' after Light and '{darkScheme}' after Dark");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("window-theme", ex.Message);
        }
    }

    private async Task<string?> ApplyThemeAndWaitForColorSchemeAsync(HermesWindowTheme theme, string expected)
    {
        _app.MainWindow.Theme = theme;

        // The web content process picks up the new color scheme asynchronously
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && _latestColorScheme != expected)
        {
            _app.MainWindow.Invoke(() => _app.MainWindow.SendMessage("color-scheme-query"));
            await Task.Delay(200);
        }

        return _latestColorScheme;
    }

    private async Task RunResizeEventTestAsync()
    {
        TestReporter.Start("resize-event");
        try
        {
            var tcs = new TaskCompletionSource<(int, int)>();

            // Use the fluent API to register the handler
            _app.MainWindow.OnResized((w, h) =>
            {
                // Only capture if we get reasonable dimensions
                if (w > 0 && h > 0)
                    tcs.TrySetResult((w, h));
            });

            // Get current size
            var (currentWidth, currentHeight) = _app.MainWindow.Size;

            // Resize to a different size
            var newWidth = currentWidth == 800 ? 900 : 800;
            var newHeight = currentHeight == 600 ? 700 : 600;

            _app.MainWindow.Invoke(() => _app.MainWindow.Size = (newWidth, newHeight));

            // Wait for resize event with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var (width, height) = await tcs.Task.WaitAsync(cts.Token);

            TestReporter.Assert("resize-event",
                width > 0 && height > 0,
                $"Resize event received with size ({width}, {height})");
        }
        catch (OperationCanceledException)
        {
            TestReporter.Fail("resize-event", "Timeout waiting for resize event");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("resize-event", ex.Message);
        }
    }

    private async Task RunMoveEventTestAsync()
    {
        TestReporter.Start("move-event");
        try
        {
            var tcs = new TaskCompletionSource<(int, int)>();

            // Use the fluent API to register the handler
            _app.MainWindow.OnMoved((x, y) =>
            {
                tcs.TrySetResult((x, y));
            });

            // Get current position
            var (currentX, currentY) = _app.MainWindow.Position;

            // Move to a different position
            var newX = currentX + 50;
            var newY = currentY + 50;

            _app.MainWindow.Invoke(() => _app.MainWindow.Position = (newX, newY));

            // Wait for move event with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var (x, y) = await tcs.Task.WaitAsync(cts.Token);

            TestReporter.Assert("move-event",
                true, // If we got here, the event fired
                $"Move event received at position ({x}, {y})");
        }
        catch (OperationCanceledException)
        {
            TestReporter.Fail("move-event", "Timeout waiting for move event");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("move-event", ex.Message);
        }
    }

    private async Task RunFocusEventTestAsync()
    {
        TestReporter.Start("focus-event");
        try
        {
            // Xvfb (headless X11) does not reliably generate focus events for minimize/restore.
            // Skip this test when running under a virtual framebuffer.
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            var isHeadless = display == ":99" ||
                             Environment.GetEnvironmentVariable("XVFB_RUNNING") == "1";

            if (isHeadless && _app.MainWindow.Platform == HermesPlatform.Linux)
            {
                Console.WriteLine("FOCUS_EVENT_TEST: Skipping - Xvfb does not support focus events");
                TestReporter.Pass("focus-event"); // Expected limitation, not a real failure
                return;
            }

            var focusOutReceived = new TaskCompletionSource<bool>();
            var focusInReceived = new TaskCompletionSource<bool>();

            // Use the fluent API to register handlers
            _app.MainWindow
                .OnFocusIn(() => focusInReceived.TrySetResult(true))
                .OnFocusOut(() => focusOutReceived.TrySetResult(true));

            // Minimize to trigger focus out, then restore to trigger focus in
            _app.MainWindow.Invoke(() => _app.MainWindow.MinimizeWindow());

            // Wait a bit for the minimize to take effect
            await Task.Delay(500);

            _app.MainWindow.Invoke(() => _app.MainWindow.RestoreWindow());

            // Wait for focus in with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            // We just need to verify one of them works - focus events are platform-specific
            // Some platforms may not fire both events on minimize/restore
            try
            {
                await Task.WhenAny(
                    focusInReceived.Task,
                    focusOutReceived.Task
                ).WaitAsync(cts.Token);

                TestReporter.Pass("focus-event");
            }
            catch (OperationCanceledException)
            {
                // If neither fired, that's a failure
                TestReporter.Fail("focus-event", "No focus events received after minimize/restore");
            }
        }
        catch (Exception ex)
        {
            TestReporter.Fail("focus-event", ex.Message);
        }
    }

    private void RunHermesSessionAutoInitTest()
    {
        TestReporter.Start("hermes-session-auto-init");
        try
        {
            var sessionId = HermesSession.AnonymousSessionId;
            var startTime = HermesSession.StartTime;
            var uptime = HermesSession.Uptime;

            var sessionIdValid = !string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out _);
            var startTimeValid = startTime > DateTimeOffset.MinValue && startTime <= DateTimeOffset.UtcNow;
            var uptimeValid = uptime > TimeSpan.Zero;

            TestReporter.Assert("hermes-session-auto-init",
                sessionIdValid && startTimeValid && uptimeValid,
                $"sessionId='{sessionId}' startTime={startTime:O} uptime={uptime}");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("hermes-session-auto-init", ex.Message);
        }
    }

    private async Task RunCrashInterceptionTestAsync()
    {
        TestReporter.Start("crash-interception");
        try
        {
            var crashReceived = new TaskCompletionSource<HermesCrashContext>();

            HermesCrashInterceptor.ProductName = "IntegrationTestApp";
            HermesCrashInterceptor.ProductVersion = "1.0.0";
            HermesCrashInterceptor.OnCrash = ctx => crashReceived.TrySetResult(ctx);
            HermesCrashInterceptor.Enable();

            // Fire-and-forget a task that throws, making it unobserved
            var faulted = Task.Run(() => throw new InvalidOperationException("integration test crash"));
            await faulted.ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
            faulted = null;

            // Force GC to trigger UnobservedTaskException
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var context = await crashReceived.Task.WaitAsync(cts.Token);

            var passed = context.Source == CrashSource.UnobservedTask
                && context.Exception.ExceptionType == "System.InvalidOperationException"
                && context.Exception.Message == "integration test crash"
                && context.Platform.ProductName == "IntegrationTestApp"
                && context.Platform.ProductVersion == "1.0.0"
                && context.Platform.DotNetVersion != null
                && context.Platform.OperatingSystem != null;

            TestReporter.Assert("crash-interception", passed,
                $"Unexpected context: Source={context.Source}, Type={context.Exception.ExceptionType}");

            HermesCrashInterceptor.Disable();
            HermesCrashInterceptor.OnCrash = null;
            HermesCrashInterceptor.ProductName = null;
            HermesCrashInterceptor.ProductVersion = null;
        }
        catch (OperationCanceledException)
        {
            TestReporter.Fail("crash-interception", "Timeout waiting for OnCrash callback");
        }
        catch (Exception ex)
        {
            TestReporter.Fail("crash-interception", ex.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
