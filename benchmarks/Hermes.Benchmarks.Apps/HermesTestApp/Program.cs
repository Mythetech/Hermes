// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Hermes;
using Hermes.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace HermesTestApp;

internal static class Program
{
    // Windows requires the main thread to be STA for WebView2
    [STAThread]
    private static async Task Main(string[] args)
    {
        // Check for fast startup mode via env var or arg
        var useFastStartup = args.Contains("--fast") || Environment.GetEnvironmentVariable("HERMES_FAST_STARTUP") == "1";

        // Start timing from the very beginning
        var sw = Stopwatch.StartNew();

        // Prewarm WebView environment (Windows only)
        HermesWindow.Prewarm();

        // Build the app with minimal configuration
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

        if (useFastStartup)
        {
            builder.UseFastStartup();
        }

        builder.ConfigureWindow(options =>
        {
            options.Title = "Hermes Benchmark App";
            options.Width = 800;
            options.Height = 600;
        });

        // Register the stopwatch so the component can report render time
        builder.Services.AddSingleton(sw);

        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        // Run the app - will block until window closes
        if (useFastStartup)
        {
            // Fast startup defers Show into RunWithFastStartup, so window
            // visibility has to be reported from the Shown event
            app.MainWindow.Shown += () => Console.WriteLine($"BENCHMARK_WINDOW:{sw.Elapsed.TotalMilliseconds:F2}");
            app.RunWithFastStartup();
        }
        else
        {
            // Build() shows the window synchronously, so it is visible by now
            Console.WriteLine($"BENCHMARK_WINDOW:{sw.Elapsed.TotalMilliseconds:F2}");
            app.Run();
        }

        await app.DisposeAsync();
    }
}
