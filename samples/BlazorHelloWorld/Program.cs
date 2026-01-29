using System.Diagnostics;
using Hermes;
using Hermes.Blazor;
using BlazorHelloWorld;
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
    [STAThread]
    public static void Main(string[] args)
    {
        var metrics = new StartupMetrics();
        metrics.Start();

        // Step 1: Prewarm WebView environment (Windows only - starts background thread)
        HermesWindow.Prewarm();
        metrics.Mark("Prewarm called");

        // Step 2: Build the app
        var builder = HermesBlazorAppBuilder.CreateDefault(args);

        builder.ConfigureWindow(options =>
        {
            options.Title = "Hermes Blazor - Hello World";
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
}
