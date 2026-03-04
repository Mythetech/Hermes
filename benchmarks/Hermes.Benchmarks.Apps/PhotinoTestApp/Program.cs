// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

// Start timing from the very beginning
var sw = Stopwatch.StartNew();

// Build the app
var builder = PhotinoBlazorAppBuilder.CreateDefault();

// Register the stopwatch so the component can report render time
builder.Services.AddSingleton(sw);

builder.RootComponents.Add<PhotinoTestApp.App>("#app");

var app = builder.Build();

// Configure window to match Hermes test app
app.MainWindow
    .SetTitle("Photino Benchmark App")
    .SetWidth(800)
    .SetHeight(600);

// Run the app - will block until window closes
app.Run();
