using System.Diagnostics;
using Hermes;
using Hermes.Blazor;
using Microsoft.Extensions.DependencyInjection;

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

builder.RootComponents.Add<HermesTestApp.App>("#app");

var app = builder.Build();

// Run the app - will block until window closes
if (useFastStartup)
{
    app.RunWithFastStartup();
}
else
{
    app.Run();
}

await app.DisposeAsync();
