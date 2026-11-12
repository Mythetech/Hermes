// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes;
using Hermes.Web;

namespace WebHelloWorld;

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
            opts.Title = "Hermes Web - Hello World";
            opts.Width = 800;
            opts.Height = 600;
            opts.DevToolsEnabled = true;
        });

#if DEBUG
        builder.UseDevServer("http://localhost:5173");
#else
        builder.UseStaticFiles("frontend/dist");
        builder.UseSpaFallback();
#endif

        var app = builder.Build();

        app.Bridge.Handle("greet", (string name) => $"Hello from C#, {name}!");
        app.Bridge.Handle("getRuntime", () => $".NET {Environment.Version}");
        app.Bridge.Handle("getPlatform", () => Environment.OSVersion.Platform.ToString());
        app.Run();
    }
}
