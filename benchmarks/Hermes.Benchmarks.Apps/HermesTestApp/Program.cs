using System.Diagnostics;
using Hermes;
using Hermes.Blazor;
using Microsoft.Extensions.DependencyInjection;

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

builder.RootComponents.Add<HermesTestApp.App>("#app");

var app = builder.Build();

// Run the app - will block until window closes
app.Run();

await app.DisposeAsync();
