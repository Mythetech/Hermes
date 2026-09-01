// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes;
using Hermes.Web;

namespace SvelteHelloWorld;

internal static class Program
{
    // Windows requires the main thread to be STA for WebView2; the attribute
    // only works on an explicit synchronous Main, never top-level statements.
    [STAThread]
    private static void Main(string[] args)
    {
        HermesWindow.Prewarm();

        var builder = HermesWebAppBuilder.Create();

        builder.ConfigureWindow(opts =>
        {
            opts.Title = "Hermes Web - Svelte";
            opts.Width = 800;
            opts.Height = 600;
            opts.DevToolsEnabled = true;
        });

#if DEBUG
        builder.UseDevServer("http://localhost:5174");
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

        var seconds = 0;
        using var ticker = new System.Threading.Timer(_ =>
        {
            seconds++;
            app.Bridge?.Send("tick", seconds);
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        app.Run();
    }
}
