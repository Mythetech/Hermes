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

        var app = builder.Build();

        app.Bridge.Handle("greet", (string name) => $"Hello from C#, {name}!");
        app.Bridge.Handle("getRuntime", () => $".NET {Environment.Version}");
        app.Bridge.Handle("getPlatform", () => Environment.OSVersion.Platform.ToString());

        var seconds = 0;
        using var ticker = new System.Threading.Timer(_ =>
        {
            seconds++;
            app.Bridge.Send("tick", seconds);
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        app.Run();
    }
}
