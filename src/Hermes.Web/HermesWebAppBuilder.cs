// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Web.Hosting;
using Hermes.Web.Interop;

namespace Hermes.Web;

public sealed class HermesWebAppBuilder
{
    private Action<HermesWindowOptions>? _windowConfiguration;
    private string? _staticFilesPath;
    private bool _spaFallback;
    private string? _devServerUrl;
    private Action<InteropBridgeOptions>? _bridgeConfiguration;

    private HermesWebAppBuilder()
    {
    }

    public static HermesWebAppBuilder Create(string[]? args = null)
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

    public HermesWebAppBuilder UseInteropBridge(Action<InteropBridgeOptions> configure)
    {
        _bridgeConfiguration = configure;
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

        // Configure interop bridge
        InteropBridge? bridge = null;
        if (_bridgeConfiguration is not null)
        {
            var bridgeOptions = new InteropBridgeOptions();
            _bridgeConfiguration(bridgeOptions);
            bridge = new InteropBridge(window.Backend, bridgeOptions);
        }

        return new HermesWebApp(window, bridge);
    }

    private static void ApplyOptions(HermesWindow window, HermesWindowOptions options)
    {
        window.SetTitle(options.Title);
        window.SetSize(options.Width, options.Height);

        if (options.X.HasValue && options.Y.HasValue)
            window.SetPosition(options.X.Value, options.Y.Value);

        if (options.CenterOnScreen)
            window.Center();

        window.SetResizable(options.Resizable);
        window.SetChromeless(options.Chromeless);
        window.SetTopMost(options.TopMost);
        window.SetDevToolsEnabled(options.DevToolsEnabled);
        window.SetContextMenuEnabled(options.ContextMenuEnabled);
        window.SetCustomTitleBar(options.CustomTitleBar);

        if (options.Maximized)
            window.Maximize();
        if (options.Minimized)
            window.Minimize();

        if (!string.IsNullOrEmpty(options.IconPath))
            window.SetIcon(options.IconPath);

        if (options.MinWidth.HasValue || options.MinHeight.HasValue)
            window.SetMinSize(options.MinWidth ?? 0, options.MinHeight ?? 0);

        if (options.MaxWidth.HasValue || options.MaxHeight.HasValue)
            window.SetMaxSize(options.MaxWidth ?? int.MaxValue, options.MaxHeight ?? int.MaxValue);

        if (options.WindowStateKey is not null)
            window.RememberWindowState(string.IsNullOrEmpty(options.WindowStateKey) ? null : options.WindowStateKey);
    }
}
