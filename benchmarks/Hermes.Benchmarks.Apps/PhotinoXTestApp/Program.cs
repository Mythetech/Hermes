// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

namespace PhotinoXTestApp;

internal static class Program
{
    // Windows requires the main thread to be STA for WebView2
    [STAThread]
    private static void Main(string[] args)
    {
        // Start timing from the very beginning
        var sw = Stopwatch.StartNew();

        // Build the app
        var builder = PhotinoBlazorAppBuilder.CreateDefault();

        // Register the stopwatch so the component can report render time
        builder.Services.AddSingleton(sw);

        builder.RootComponents.Add<App>("#app");

        var app = builder.Build();

        // Configure window to match Hermes test app
        app.MainWindow
            .SetTitle("PhotinoX Benchmark App")
            .SetWidth(800)
            .SetHeight(600);

        // Photino creates and shows the native window during Run; the PhotinoX
        // fork renamed WindowCreated to Created
        app.MainWindow.Created += (_, _) => Console.WriteLine($"BENCHMARK_WINDOW:{sw.Elapsed.TotalMilliseconds:F2}");

        // Run the app - will block until window closes
        app.Run();
    }
}
