// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Web.Hosting;
using Hermes.Web.Interop;

namespace Hermes.Web;

public sealed class HermesWebAppBuilder
{
    private Action<HermesWindowOptions>? _windowConfiguration;
    private string? _staticFilesPath;
    private bool _spaFallback;
    private string? _devServerUrl;

    private HermesWebAppBuilder()
    {
    }

    public static HermesWebAppBuilder Create()
    {
        return new HermesWebAppBuilder();
    }

    public HermesWebAppBuilder ConfigureWindow(Action<HermesWindowOptions> configure)
    {
        _windowConfiguration = configure;
        return this;
    }

    public HermesWebAppBuilder UseStaticFiles(string? path = null)
    {
        _staticFilesPath = path ?? "wwwroot";
        return this;
    }

    public HermesWebAppBuilder UseSpaFallback()
    {
        _spaFallback = true;
        return this;
    }

    public HermesWebAppBuilder UseDevServer(string url)
    {
        _devServerUrl = url;
        return this;
    }

    public HermesWebApp Build()
    {
        var window = new HermesWindow();

        if (_windowConfiguration is not null)
        {
            var options = new HermesWindowOptions();
            _windowConfiguration(options);
            ApplyOptions(window, options);
        }

        var useDevServer = _devServerUrl is not null;

        if (useDevServer && _staticFilesPath is not null)
        {
            Console.WriteLine("[Hermes.Web] Both UseDevServer() and UseStaticFiles() configured. Dev server takes priority.");
        }

        // Register custom scheme for static files (required before Initialize on macOS)
        StaticFileHost? staticFileHost = null;

        if (!useDevServer && _staticFilesPath is not null)
        {
            var staticDir = Path.IsPathRooted(_staticFilesPath)
                ? _staticFilesPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _staticFilesPath);

            staticFileHost = new StaticFileHost(staticDir, _spaFallback);

            if (OperatingSystem.IsWindows())
            {
                window.RegisterCustomScheme("http", staticFileHost.HandleRequest);
            }
            else
            {
                window.RegisterCustomScheme("app", staticFileHost.HandleRequest);
            }
        }
        else if (!OperatingSystem.IsWindows())
        {
            // Pre-register scheme even if not used (macOS requires this before Initialize)
            window.RegisterCustomScheme("app", _ => (null, null));
        }

        // Set navigation target
        if (useDevServer)
        {
            window.Load(_devServerUrl!);
        }
        else
        {
            var baseUri = OperatingSystem.IsWindows()
                ? "http://localhost/"
                : "app://localhost/";
            window.Load(baseUri);
        }

        return new HermesWebApp(window, new InteropBridge(window.Backend));
    }

    private static void ApplyOptions(HermesWindow window, HermesWindowOptions options) =>
        HermesWindowOptions.ApplyTo(window, options);
}
