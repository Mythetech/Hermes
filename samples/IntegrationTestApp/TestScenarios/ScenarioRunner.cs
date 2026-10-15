// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using System.Text;
using Hermes;
using Hermes.Abstractions;
using Hermes.Blazor;
using Hermes.Contracts.DataTransfer;
using Hermes.Contracts.Diagnostics;
using Hermes.Contracts.Notifications;
using Hermes.Contracts.Plugins;
using Hermes.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

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

        // Platform visibility tests - these verify real backend behavior
        // Run resize/move/focus FIRST - they don't need WebView to be ready
        await RunResizeEventTestAsync();
        await RunMoveEventTestAsync();
        await RunFocusEventTestAsync();

        // Diagnostics session auto-init (must run before anything that overrides AnonymousSessionId)
        RunHermesSessionAutoInitTest();

        // Crash interception test
        await RunCrashInterceptionTestAsync();

        // Notifications: exercises the real backend, supported or not, without needing the WebView
        await RunNotificationsTestAsync();

        // Clipboard: the real OS clipboard, no WebView needed
        RunClipboardTextTest();
        if (RichClipboardSupported)
        {
            RunClipboardRichRoundTripTest();
            if (OperatingSystem.IsWindows())
                RunClipboardBusyTest();
            await RunClipboardChangedTestAsync();
            await RunClipboardChangedMixedThreadsTestAsync();
        }

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

            // Close must happen on the UI thread
            _app.MainWindow.Invoke(() => _app.MainWindow.Close());

            // Backstop: if Close() doesn't terminate the app within 5 seconds, force exit.
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

    private async Task RunNotificationsTestAsync()
    {
        const string testName = "notifications";
        TestReporter.Start(testName);
        try
        {
            // Backends create thread-affine resources on first access, and this runner is on a thread-pool thread.
            _app.MainWindow.Invoke(() => _ = HermesApplication.Notifications.IsSupported);

            var notifications = _app.Services.GetRequiredService<INativeNotifications>();
            var notification = new NativeNotification
            {
                Title = "Hermes integration test",
                Body = "This notification is dismissed immediately.",
                Tag = "integration/notifications",
                Silent = true,
            };

            if (!notifications.IsSupported)
            {
                Console.WriteLine($"  Notifications unsupported on this host: {notifications.UnsupportedReason}");
                await notifications.ShowAsync(notification);
                TestReporter.Assert(testName,
                    !string.IsNullOrWhiteSpace(notifications.UnsupportedReason),
                    "UnsupportedReason must be set when IsSupported is false");
                return;
            }

            Console.WriteLine("  Notifications supported; showing and dismissing one");
            try
            {
                await notifications.ShowAsync(notification);
            }
            catch (InvalidOperationException ex) when (Environment.GetEnvironmentVariable("HERMES_CI") == "1")
            {
                // Hosted Windows runners have no interactive shell session and may refuse Show.
                // Rendering is verified manually via the NotificationsDemo sample, not here.
                Console.WriteLine($"  Platform refused Show under CI, accepted as pass: {ex.Message}");
                TestReporter.Pass(testName);
                return;
            }

            notifications.Dismiss(notification.Id);
            TestReporter.Pass(testName);
        }
        catch (Exception ex)
        {
            TestReporter.Fail(testName, ex.Message);
        }
    }

    private void RunClipboardTextTest()
    {
        const string testName = "clipboard-text";
        TestReporter.Start(testName);

        try
        {
            var expected = $"hermes-clipboard-{Guid.NewGuid():N}";
            Clipboard.SetText(expected);
            var actual = Clipboard.GetText();

            TestReporter.Assert(testName, actual == expected, $"Expected '{expected}', got '{actual}'");
        }
        catch (Exception ex)
        {
            TestReporter.Fail(testName, ex.Message);
        }
    }

    // Platforms whose clipboard backend reads and writes every format.
    private static bool RichClipboardSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    private void RunClipboardRichRoundTripTest()
    {
        const string testName = "clipboard-rich-roundtrip";
        const string customType = "application/x-hermes-integration";
        TestReporter.Start(testName);
        var directory = Path.Combine(Path.GetTempPath(), "hermes clipboard #1");

        try
        {
            var failures = new List<string>();
            void Check(string what, bool ok)
            {
                if (!ok)
                    failures.Add(what);
            }

            Directory.CreateDirectory(directory);
            var files = new[] { "a.txt", "b é.txt", "c#d%.txt" }.Select(name => Path.Combine(directory, name)).ToArray();
            foreach (var file in files)
                File.WriteAllText(file, "x");

            Clipboard.Write(new HermesDataPackage { Text = "plain", Html = "<b>rich é</b>" });
            var html = Clipboard.Read(HermesDataFormats.Text | HermesDataFormats.Html);
            Check("text", html.Text == "plain");
            Check("html", html.Html == "<b>rich é</b>");
            Check("formats after html", Clipboard.GetFormats().HasFlag(HermesDataFormats.Html));

            foreach (var (label, png) in new[] { ("small png", TestPng.Create(16, 16, 1)), ("8 MB png", TestPng.Create(1448, 1448, 2)) })
            {
                Clipboard.Write(new HermesDataPackage { Png = new ReadOnlyMemory<byte>(png) });
                var image = Clipboard.Read(HermesDataFormats.Png);
                Check(label, image.Png is { } read && read.Span.SequenceEqual(png));
            }
            Check("legacy image format", HasLegacyImageFormat());

            Clipboard.Write(new HermesDataPackage { Text = "with files", Files = files });
            var withFiles = Clipboard.Read(HermesDataFormats.Text | HermesDataFormats.Files);
            Check("text beside files", withFiles.Text == "with files");
            Check("files", withFiles.Files is { } readFiles && readFiles.SequenceEqual(files));
            Check("formats after files", Clipboard.GetFormats().HasFlag(HermesDataFormats.Files));

            // Finder puts each file's icon beside the URLs as TIFF, which must not read back as an image.
            Clipboard.Write(new HermesDataPackage
            {
                Files = files,
                Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["image/tiff"] = new byte[] { 1, 2, 3 } },
            });
            Check("file icon not png", !Clipboard.GetFormats().HasFlag(HermesDataFormats.Png)
                && Clipboard.Read(HermesDataFormats.Png).Png is null);

            Clipboard.Write(new HermesDataPackage
            {
                Text = "pdf",
                Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/pdf"] = Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF") },
            });
            Check("pdf not png", !Clipboard.GetFormats().HasFlag(HermesDataFormats.Png));

            // macOS reads Cocoa RTF back as HTML; other platforms keep RTF as a custom format.
            if (OperatingSystem.IsMacOS())
            {
                Clipboard.Write(new HermesDataPackage
                {
                    Text = "bold plain",
                    Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["text/rtf"] = Encoding.UTF8.GetBytes(@"{\rtf1\ansi {\b bold} plain}") },
                });
                Check("formats after rtf", Clipboard.GetFormats().HasFlag(HermesDataFormats.Html));
                Check("rtf as html", Clipboard.Read(HermesDataFormats.Html).Html is { } rtfHtml && rtfHtml.Contains("bold"));
            }

            var customBytes = new byte[] { 1, 2, 3, 250 };
            Clipboard.Write(new HermesDataPackage
            {
                Custom = new Dictionary<string, ReadOnlyMemory<byte>> { [customType] = customBytes },
            });
            Check("has custom", Clipboard.HasCustom(customType));
            Check("has custom ignoring case", Clipboard.HasCustom(customType.ToUpperInvariant()));
            var custom = Clipboard.Read(HermesDataFormats.None, [customType]);
            Check("custom", custom.Custom.TryGetValue(customType, out var readCustom) && readCustom.Span.SequenceEqual(customBytes));

            // GTK converts other image targets to PNG, and X11 target names are case-sensitive.
            if (OperatingSystem.IsLinux())
            {
                Clipboard.Write(new HermesDataPackage
                {
                    Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["image/bmp"] = TestBmp.Create2x2() },
                });
                Check("bmp as png", Clipboard.Read(HermesDataFormats.Png).Png is { } bmpPng
                    && bmpPng.Span.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));

                Clipboard.Write(new HermesDataPackage
                {
                    Custom = new Dictionary<string, ReadOnlyMemory<byte>> { ["application/x-hermes-case"] = new byte[] { 1 } },
                });
                var upper = Clipboard.Read(HermesDataFormats.None, ["APPLICATION/X-HERMES-CASE"]);
                Check("custom read ignoring case", upper.Custom.TryGetValue("APPLICATION/X-HERMES-CASE", out var upperBytes)
                    && upperBytes.Span.SequenceEqual(new byte[] { 1 }));
            }

            Clipboard.Clear();
            Check("clear", Clipboard.GetFormats() == HermesDataFormats.None);

            TestReporter.Assert(testName, failures.Count == 0, "Failed: " + string.Join(", ", failures));
        }
        catch (Exception ex)
        {
            TestReporter.Fail(testName, ex.Message);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private void RunClipboardBusyTest()
    {
        const string testName = "clipboard-busy";
        TestReporter.Start(testName);

        try
        {
            // Holds the clipboard open for three seconds from another process, as clipboard managers do.
            var script = Path.Combine(Path.GetTempPath(), "hermes-hold-clipboard.ps1");
            File.WriteAllText(script, """
                Add-Type -Namespace W -Name U -MemberDefinition '[DllImport("user32.dll")] public static extern bool OpenClipboard(System.IntPtr h); [DllImport("user32.dll")] public static extern bool CloseClipboard();'
                [W.U]::OpenClipboard([IntPtr]::Zero) | Out-Null
                'held'
                Start-Sleep -Seconds 3
                [W.U]::CloseClipboard() | Out-Null
                """);
            using var holder = Process.Start(new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            holder.StandardOutput.ReadLine();

            var watch = Stopwatch.StartNew();
            var threw = false;
            try
            {
                Clipboard.SetText("busy");
            }
            catch (IOException)
            {
                threw = true;
            }
            watch.Stop();
            holder.WaitForExit(10_000);

            TestReporter.Assert(testName, threw && watch.ElapsedMilliseconds < 1000,
                $"Expected IOException within 1s while another process held the clipboard; threw={threw}, took {watch.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            TestReporter.Fail(testName, ex.Message);
        }
    }

    private async Task RunClipboardChangedTestAsync()
    {
        const string testName = "clipboard-changed";
        TestReporter.Start(testName);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action handler = () => changed.TrySetResult();

        try
        {
            Clipboard.Changed += handler;

            // macOS takes its baseline change count on the main thread, asynchronously
            await Task.Delay(TimeSpan.FromSeconds(1));
            ExternalClipboard.WriteText($"hermes-external-{Guid.NewGuid():N}");

            var raised = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(10))) == changed.Task;
            TestReporter.Assert(testName, raised, "Changed was not raised within 10s of an external write");
        }
        catch (Exception ex)
        {
            TestReporter.Fail(testName, ex.Message);
        }
        finally
        {
            Clipboard.Changed -= handler;
        }
    }

    private async Task RunClipboardChangedMixedThreadsTestAsync()
    {
        const string testName = "clipboard-changed-mixed-threads";
        TestReporter.Start(testName);
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action first = () => { };
        Action second = () => changed.TrySetResult();

        try
        {
            Clipboard.Changed += first;
            await Task.Delay(TimeSpan.FromSeconds(1));

            // The background unsubscribe queues its stop behind the main thread, which subscribes again before it runs.
            _app.MainWindow.Invoke(() =>
            {
                Task.Run(() => Clipboard.Changed -= first).Wait();
                Clipboard.Changed += second;
            });

            await Task.Delay(TimeSpan.FromSeconds(1));
            ExternalClipboard.WriteText($"hermes-external-{Guid.NewGuid():N}");

            var raised = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(10))) == changed.Task;
            TestReporter.Assert(testName, raised, "Changed was not raised after a background unsubscribe and a main-thread subscribe");
        }
        catch (Exception ex)
        {
            TestReporter.Fail(testName, ex.Message);
        }
        finally
        {
            Clipboard.Changed -= first;
            Clipboard.Changed -= second;
        }
    }

    // Older apps paste the legacy format: TIFF on macOS, a DIB bitmap on Windows. Linux has none.
    private static bool HasLegacyImageFormat()
    {
        if (OperatingSystem.IsMacOS())
            return Clipboard.HasCustom("image/tiff");

        if (OperatingSystem.IsWindows())
        {
            // Windows PowerShell 5.1 runs STA, which System.Windows.Forms.Clipboard requires.
            using var process = Process.Start(new ProcessStartInfo("powershell",
                "-NoProfile -Sta -Command \"Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::ContainsImage()\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            })!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
