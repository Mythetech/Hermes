// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Contracts.Notifications;
using Hermes.Contracts.Plugins;
using Hermes.Diagnostics;
using Hermes.Hosting;
using Hermes.Plugins;
using Hermes.Web.Hosting;
using Hermes.Web.Interop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hermes.Web;

/// <summary>
/// Builder for configuring and creating a Hermes app whose page is a JavaScript or TypeScript frontend.
/// </summary>
public sealed class HermesWebAppBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;
    private readonly HermesFrontendOptions _frontendOptions = new();
    private Action<HermesWindowOptions>? _windowConfiguration;
    private Uri? _devServer;

    private HermesWebAppBuilder(string[]? args, bool addDefaultConfiguration)
    {
        _hostBuilder = HermesHostDefaults.CreateBuilder(args);

        if (addDefaultConfiguration)
            HermesHostDefaults.AddDefaultConfiguration(_hostBuilder, args);

        HermesHostDefaults.AddWindowHost(_hostBuilder.Services);
        HermesSmokeHosting.UseNoOpSession(_hostBuilder.Services);
    }

    /// <summary>
    /// Creates a builder with appsettings.json, the environment's appsettings file, environment variables
    /// and the command line as configuration.
    /// </summary>
    public static HermesWebAppBuilder CreateDefault(string[]? args = null) => new(args, addDefaultConfiguration: true);

    /// <summary>
    /// Creates a builder with no configuration sources; add them through <see cref="Configuration"/>.
    /// </summary>
    public static HermesWebAppBuilder CreateSlimBuilder(string[]? args = null) => new(args, addDefaultConfiguration: false);

    /// <summary>Gets the service collection for adding the app's services.</summary>
    public IServiceCollection Services => _hostBuilder.Services;

    /// <summary>Gets the configuration manager for adding configuration sources.</summary>
    public IConfigurationManager Configuration => _hostBuilder.Configuration;

    /// <summary>Gets the logging builder for configuring logging providers.</summary>
    public ILoggingBuilder Logging => _hostBuilder.Logging;

    /// <summary>Gets the metrics builder for configuring metrics.</summary>
    public IMetricsBuilder Metrics => _hostBuilder.Metrics;

    /// <summary>Gets the host environment. Development is read from DOTNET_ENVIRONMENT or --environment.</summary>
    public IHostEnvironment Environment => _hostBuilder.Environment;

    /// <inheritdoc />
    IDictionary<object, object> IHostApplicationBuilder.Properties =>
        ((IHostApplicationBuilder)_hostBuilder).Properties;

    /// <inheritdoc />
    void IHostApplicationBuilder.ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure) =>
        ((IHostApplicationBuilder)_hostBuilder).ConfigureContainer(factory, configure);

    /// <summary>Replaces the probe that checks whether the dev server answers, so tests need no server.</summary>
    internal Func<Uri, TimeSpan, bool> ProbeDevServer { get; set; } = DevServerProbe.IsListening;

    /// <summary>
    /// Configures the main window.
    /// </summary>
    public HermesWebAppBuilder ConfigureWindow(Action<HermesWindowOptions> configure)
    {
        _windowConfiguration = configure;
        return this;
    }

    /// <summary>
    /// Chooses where the page comes from. In the Development environment the window opens
    /// <paramref name="devServer"/> when it answers; otherwise, and always outside Development, it serves the
    /// built frontend from <see cref="HermesFrontendOptions.StaticFilesPath"/>. Without this call the app
    /// serves wwwroot.
    /// </summary>
    /// <param name="devServer">The dev server's absolute http or https URL, such as http://localhost:5173.</param>
    /// <param name="configure">Changes the static files folder, SPA fallback or probe timeout.</param>
    /// <exception cref="ArgumentException"><paramref name="devServer"/> is not an absolute http or https URL.</exception>
    public HermesWebAppBuilder UseFrontend(string? devServer = null, Action<HermesFrontendOptions>? configure = null)
    {
        if (devServer is not null)
        {
            if (!Uri.TryCreate(devServer, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException($"The dev server must be an absolute http or https URL, such as http://localhost:5173, not '{devServer}'.", nameof(devServer));

            _devServer = url;
        }

        configure?.Invoke(_frontendOptions);
        return this;
    }

    /// <summary>
    /// Builds the app. Register bridge handlers on <see cref="HermesWebApp.Bridge"/> before calling <see cref="HermesWebApp.Run"/>.
    /// </summary>
    public HermesWebApp Build() => BuildCore(new HermesWindow());

    /// <summary>Runs the real Build() sequence against a supplied backend.</summary>
    internal static HermesWebApp BuildForTest(HermesWebAppBuilder builder, IHermesWindowBackend backend) =>
        builder.BuildCore(new HermesWindow(backend));

    private HermesWebApp BuildCore(HermesWindow window)
    {
        if (_windowConfiguration is not null)
        {
            var options = new HermesWindowOptions();
            _windowConfiguration(options);
            HermesWindowOptions.ApplyTo(window, options);
        }

        var bridge = new InteropBridge(window.Backend);

        Services.AddSingleton(window);
        Services.AddSingleton(bridge);
        Services.AddSingleton<IConfiguration>(Configuration);
        Services.AddSingleton<IClipboard, DesktopClipboard>();
        Services.AddSingleton<IDragDrop>(new DesktopDragDrop(window));
        Services.AddSingleton<IGlobalHotkeys, DesktopGlobalHotkeys>();
        Services.AddSingleton<INativeNotifications, DesktopNativeNotifications>();

        var host = _hostBuilder.Build();

        var staticRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, _frontendOptions.StaticFilesPath));
        var source = FrontendResolver.Resolve(
            Environment.IsDevelopment(), _devServer, staticRoot, _frontendOptions.DevServerProbeTimeout, ProbeDevServer);
        LoadFrontend(window, source);

        return new HermesWebApp(host, window, bridge);
    }

    private void LoadFrontend(HermesWindow window, FrontendSource source)
    {
        if (source is FrontendSource.StaticFiles staticFiles)
        {
            var host = new StaticFileHost(staticFiles.Root, _frontendOptions.SpaFallback);
            var scheme = OperatingSystem.IsWindows() ? "http" : "app";
            window.RegisterCustomScheme(scheme, host.HandleRequest);
            window.Load($"{scheme}://localhost/");
            HermesLogger.Info($"Serving the frontend from {staticFiles.Root}");
            return;
        }

        // macOS and Linux register custom schemes before the WebView is created, whether or not they are used.
        if (!OperatingSystem.IsWindows())
            window.RegisterCustomScheme("app", _ => (null, null));

        switch (source)
        {
            case FrontendSource.DevServer devServer:
                window.Load(devServer.Url.AbsoluteUri);
                HermesLogger.Info($"Loading the frontend from the dev server at {devServer.Url.AbsoluteUri}");
                break;
            case FrontendSource.Missing missing:
                window.LoadHtml(missing.Html);
                HermesLogger.Warning("No frontend to load: the dev server did not answer and there is no built frontend");
                break;
        }
    }
}
