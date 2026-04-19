// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes;
using Hermes.Web;

HermesWindow.Prewarm();

var builder = HermesWebAppBuilder.Create(args);

builder.ConfigureWindow(opts =>
{
    opts.Title = "Hermes Web - React Hello World";
    opts.Width = 800;
    opts.Height = 600;
    opts.DevToolsEnabled = true;
});

#if DEBUG
builder.UseDevServer("http://localhost:5176");
#else
builder.UseStaticFiles("frontend/dist");
builder.UseSpaFallback();
#endif

builder.UseInteropBridge(bridge =>
{
    bridge.Register<string, string>("greet", name => $"Hello from C#, {name}!");
    bridge.Register("getRuntime", () => $".NET {Environment.Version}");
    bridge.Register("getPlatform", () => Environment.OSVersion.Platform.ToString());
});

var app = builder.Build();
app.Run();
