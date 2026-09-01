// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Hermes;
using Hermes.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace HermesTestApp;

internal static class Program
{
    // Windows requires the main thread to be STA for WebView2. The attribute
    // must sit on a synchronous Main: on an async Main the runtime reads the
    // apartment from the compiler-generated entry point wrapper, which does
    // not inherit [STAThread], so the thread silently starts MTA.
    [STAThread]
    private static void Main(string[] args)
    {
        // Start timing from the very beginning
        var sw = Stopwatch.StartNew();

        // Prewarm WebView environment (Windows only)
        HermesWindow.Prewarm();

        // Build the app with minimal configuration
        var builder = HermesBlazorAppBuilder.CreateSlimBuilder();

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

        // Build() shows the window synchronously, so it is visible by now
        Console.WriteLine($"BENCHMARK_WINDOW:{sw.Elapsed.TotalMilliseconds:F2}");

        // Run the app - will block until window closes
        app.Run();

        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
