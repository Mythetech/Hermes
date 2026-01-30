using System.Diagnostics;
using Hermes;
using Hermes.Blazor;
using BlazorHelloWorld;
using Hermes.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

// ============================================================================
// STARTUP METRICS - Tracks time from Main() to various milestones
// ============================================================================

public sealed class StartupMetrics
{
    private readonly Stopwatch _stopwatch = new();
    private readonly List<(string Name, TimeSpan Elapsed)> _marks = new();
    private bool _firstRenderReported;

    public void Start() => _stopwatch.Start();

    public void Mark(string name)
    {
        _marks.Add((name, _stopwatch.Elapsed));
    }

    public void ReportFirstRender()
    {
        if (_firstRenderReported) return;
        _firstRenderReported = true;

        var elapsed = _stopwatch.Elapsed;
        _stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine($"🚀 FIRST RENDER: {elapsed.TotalMilliseconds:F2}ms from Main()");
        Console.WriteLine();
    }

    public void PrintMarks()
    {
        TimeSpan previous = TimeSpan.Zero;
        foreach (var (name, elapsed) in _marks)
        {
            var delta = elapsed - previous;
            Console.WriteLine($"║  {elapsed.TotalMilliseconds,7:F2}ms (+{delta.TotalMilliseconds,6:F2}ms) │ {name,-25} ║");
            previous = elapsed;
        }
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;
}

public static class Program
{
    private static int _windowNumber = 1;

    [STAThread]
    public static void Main(string[] args)
    {
        // Parse command-line arguments
        var cliParser = new CommandLineParser(args);

        // Check if we're being launched as a secondary window
        if (cliParser.TryGetValue("window-number", out var windowNumStr) &&
            int.TryParse(windowNumStr, out var windowNum))
        {
            _windowNumber = windowNum;
        }

        var metrics = new StartupMetrics();
        metrics.Start();

        // Step 1: Prewarm WebView environment (Windows only - starts background thread)
        HermesWindow.Prewarm();
        metrics.Mark("Prewarm called");

        // Step 2: Build the app
        var builder = HermesBlazorAppBuilder.CreateDefault(args);

        builder.ConfigureWindow(options =>
        {
            options.Title = _windowNumber > 1
                ? $"Hermes Blazor - Window {_windowNumber}"
                : "Hermes Blazor - Hello World";
            options.Width = 1024;
            options.Height = 768;
            options.CenterOnScreen = true;
            options.DevToolsEnabled = true;
            options.CustomTitleBar = true;  // Enable custom title bar
        });

        // Register the metrics service so components can report render time
        builder.Services.AddSingleton(metrics);

        builder.RootComponents.Add<App>("#app");

        metrics.Mark("Builder configured");

        var app = builder.Build();
        metrics.Mark("App built");

        // Step 3: Configure menus
        app.MainWindow.MenuBar
            .AddMenu("File", file =>
            {
                file.AddItem("New", "file.new", item => item.WithAccelerator("Ctrl+N"))
                    .AddItem("Open...", "file.open", item => item.WithAccelerator("Ctrl+O"))
                    .AddSeparator()
                    .AddItem("Save", "file.save", item => item.WithAccelerator("Ctrl+S"))
                    .AddItem("Save As...", "file.saveAs", item => item.WithAccelerator("Ctrl+Shift+S"))
                    .AddSeparator()
                    .AddItem("Exit", "file.exit", item => item.WithAccelerator("Alt+F4"));
            })
            .AddMenu("Edit", edit =>
            {
                edit.AddItem("Undo", "edit.undo", item => item.WithAccelerator("Ctrl+Z"))
                    .AddItem("Redo", "edit.redo", item => item.WithAccelerator("Ctrl+Y"))
                    .AddSeparator()
                    .AddItem("Cut", "edit.cut", item => item.WithAccelerator("Ctrl+X"))
                    .AddItem("Copy", "edit.copy", item => item.WithAccelerator("Ctrl+C"))
                    .AddItem("Paste", "edit.paste", item => item.WithAccelerator("Ctrl+V"))
                    .AddSeparator()
                    .AddItem("Select All", "edit.selectAll", item => item.WithAccelerator("Ctrl+A"));
            })
            .AddMenu("View", view =>
            {
                view.AddItem("Zoom In", "view.zoomIn", item => item.WithAccelerator("Ctrl++"))
                    .AddItem("Zoom Out", "view.zoomOut", item => item.WithAccelerator("Ctrl+-"))
                    .AddItem("Reset Zoom", "view.zoomReset", item => item.WithAccelerator("Ctrl+0"))
                    .AddSeparator()
                    .AddItem("Toggle Full Screen", "view.fullScreen", item => item.WithAccelerator("F11"));
            })
            .AddMenu("Help", help =>
            {
                help.AddItem("About Hermes", "help.about");
            });

        // Handle menu item clicks
        app.MainWindow.MenuBar.ItemClicked += itemId =>
        {
            Console.WriteLine($"Menu item clicked: {itemId}");

            if (itemId == "file.exit")
            {
                app.MainWindow.Close();
            }
        };

        metrics.Mark("Menus configured");

        // Step 3b: Configure dock menu (macOS only)
        if (HermesApplication.DockMenu is { } dockMenu)
        {
            dockMenu
                .AddItem("New Window", "dock.newWindow")
                .AddSeparator()
                .AddSubmenu("Recent Files", "dock.recent", submenu =>
                {
                    submenu
                        .AddItem("Document1.txt", "dock.recent.doc1")
                        .AddItem("Document2.txt", "dock.recent.doc2")
                        .AddItem("Document3.txt", "dock.recent.doc3")
                        .AddSeparator()
                        .AddItem("Clear Recent", "dock.recent.clear");
                });

            dockMenu.ItemClicked += itemId =>
            {
                Console.WriteLine($"Dock menu item clicked: {itemId}");

                if (itemId == "dock.newWindow")
                {
                    // Spawn a new process for multi-window support
                    // This provides process isolation - one window crash doesn't affect others
                    SpawnNewWindow();
                }

                if (itemId == "dock.recent.clear")
                {
                    // Clear the recent files submenu
                    if (dockMenu.TryGetSubmenu("dock.recent", out var recentSubmenu))
                    {
                        recentSubmenu?.Clear();
                        recentSubmenu?.AddItem("(No recent files)", "dock.recent.empty", item => item.WithEnabled(false));
                    }
                }
            };

            metrics.Mark("Dock menu configured");
        }

        // Step 4: Run
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           HERMES BLAZOR STARTUP METRICS                    ║");
        Console.WriteLine("╠════════════════════════════════════════════════════════════╣");
        metrics.PrintMarks();
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Running... (first render time will be reported by the component)");
        Console.WriteLine();

        app.Run();

        // Cleanup
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();

        Console.WriteLine("Window closed. Goodbye!");
    }

    /// <summary>
    /// Spawns a new instance of the application in a separate process.
    /// This provides process isolation - one window crash doesn't affect others.
    /// </summary>
    private static void SpawnNewWindow()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName
                       ?? Environment.ProcessPath;

            if (string.IsNullOrEmpty(exePath))
            {
                Console.WriteLine("ERROR: Could not determine executable path");
                return;
            }

            // Track window numbers for demonstration
            var nextWindowNumber = _windowNumber + 1;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--window-number {nextWindowNumber}",
                UseShellExecute = true // Required for macOS to spawn as new app instance
            };

            Console.WriteLine($"Spawning new window: {exePath} --window-number {nextWindowNumber}");
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to spawn new window: {ex.Message}");
        }
    }
}
