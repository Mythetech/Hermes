// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes;
using Hermes.Web;
using Microsoft.Extensions.DependencyInjection;

namespace SvelteHelloWorld;

internal static class Program
{
    // Windows requires the main thread to be STA for WebView2; the attribute
    // only works on an explicit synchronous Main, never top-level statements.
    [STAThread]
    private static void Main(string[] args)
    {
        HermesWindow.Prewarm();

        var builder = HermesWebAppBuilder.CreateDefault(args);

        builder.ConfigureWindow(opts =>
        {
            opts.Title = "Hermes Web - Svelte";
            opts.Width = 800;
            opts.Height = 600;
            opts.DevToolsEnabled = true;
        });

        builder.UseFrontend(devServer: "http://localhost:5174");
        builder.Services.AddHostedService<TickService>();

        var app = builder.Build();

        app.Bridge.Handle("greet", (string name) => $"Hello from C#, {name}!");
        app.Bridge.Handle("getRuntime", () => $".NET {Environment.Version}");
        app.Bridge.Handle("getPlatform", () => Environment.OSVersion.Platform.ToString());

        app.Run();
        app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
