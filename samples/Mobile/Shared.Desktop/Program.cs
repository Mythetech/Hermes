// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes;
using Hermes.Blazor;
using Shared.App;

HermesWindow.Prewarm();

var builder = HermesBlazorAppBuilder.CreateDefault(args);
builder.ConfigureWindow(opts =>
{
    opts.Title = "Shared Blazor — Desktop";
    opts.Width = 900;
    opts.Height = 700;
    opts.CenterOnScreen = true;
    opts.DevToolsEnabled = true;
});
builder.RootComponents.Add<App>("#app");

var app = builder.Build();
app.Run();
await app.DisposeAsync();
