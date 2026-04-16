// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Diagnostics.CodeAnalysis;
using Hermes.Abstractions;
using Hermes.Blazor.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Components.WebView;
using Hermes.Blazor.DevServer;

namespace Hermes.Blazor;

/// <summary>
/// Builder for configuring and creating a Hermes Blazor application.
/// </summary>
public sealed class HermesBlazorAppBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _hostBuilder;
    private readonly List<RootComponentRegistration> _rootComponents = new();
    private IFileProvider? _fileProvider;
    private Action<HermesWindowOptions>? _windowConfiguration;
    private string _hostPage = "index.html";
    private string? _loadingHtml;
    private bool _deferWindowShow;
    private bool? _forceDevServer;

    private HermesBlazorAppBuilder(string[]? args, bool addDefaultConfiguration)
    {
        _hostBuilder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            DisableDefaults = true
        });

        if (addDefaultConfiguration)
        {
            _hostBuilder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{_hostBuilder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args ?? []);
        }
    }

    /// <summary>
    /// Creates a new builder with default configuration including appsettings.json,
    /// environment variables, and command-line arguments.
    /// </summary>
    public static HermesBlazorAppBuilder CreateDefault(string[]? args = null)
    {
        return new HermesBlazorAppBuilder(args, addDefaultConfiguration: true);
    }

    /// <summary>
    /// Creates a new builder with minimal configuration. No configuration sources
    /// are added by default; add them manually via the Configuration property.
    /// </summary>
    public static HermesBlazorAppBuilder CreateSlimBuilder(string[]? args = null)
    {
        return new HermesBlazorAppBuilder(args, addDefaultConfiguration: false);
    }

    /// <summary>
    /// Gets the service collection for adding custom services.
    /// </summary>
    public IServiceCollection Services => _hostBuilder.Services;

    /// <summary>
    /// Gets the configuration manager for adding configuration sources.
    /// </summary>
    public IConfigurationManager Configuration => _hostBuilder.Configuration;

    /// <summary>
    /// Gets the logging builder for configuring logging providers.
    /// </summary>
    public ILoggingBuilder Logging => _hostBuilder.Logging;

    /// <summary>
    /// Gets the metrics builder for configuring metrics.
    /// </summary>
    public IMetricsBuilder Metrics => _hostBuilder.Metrics;

    /// <summary>
    /// Gets the host environment information.
    /// </summary>
    public IHostEnvironment Environment => _hostBuilder.Environment;

    /// <inheritdoc />
    IDictionary<object, object> IHostApplicationBuilder.Properties =>
        ((IHostApplicationBuilder)_hostBuilder).Properties;

    /// <inheritdoc />
    void IHostApplicationBuilder.ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure) =>
        ((IHostApplicationBuilder)_hostBuilder).ConfigureContainer(factory, configure);

    /// <summary>
    /// Gets the root components collection for adding Blazor components during build.
    /// </summary>
    public RootComponentCollection RootComponents { get; } = new();

    /// <summary>
    /// Configures the file provider for serving static files.
    /// </summary>
    public HermesBlazorAppBuilder UseFileProvider(IFileProvider fileProvider)
    {
        _fileProvider = fileProvider;
        return this;
    }

    /// <summary>
    /// Configures the host page (default: index.html).
    /// </summary>
    public HermesBlazorAppBuilder UseHostPage(string hostPage)
    {
        _hostPage = hostPage;
        return this;
    }

    /// <summary>
    /// Configures the main window.
    /// </summary>
    public HermesBlazorAppBuilder ConfigureWindow(Action<HermesWindowOptions> configure)
    {
        _windowConfiguration = configure;
        return this;
    }

    /// <summary>
    /// Sets custom HTML to display during fast startup loading.
    /// This HTML is shown immediately when using <see cref="HermesBlazorApp.RunWithFastStartup"/>,
    /// before Blazor components are initialized.
    /// </summary>
    /// <param name="html">Custom HTML to display. If null, a default spinner is used.</param>
    public HermesBlazorAppBuilder UseLoadingHtml(string? html)
    {
        _loadingHtml = html;
        return this;
    }

    /// <summary>
    /// Configures the builder to defer showing the window until <see cref="HermesBlazorApp.Run"/>
    /// or <see cref="HermesBlazorApp.RunWithFastStartup"/> is called. This is required for
    /// fast startup mode to work properly.
    /// </summary>
    public HermesBlazorAppBuilder UseFastStartup()
    {
        _deferWindowShow = true;
        return this;
    }

    /// <summary>
    /// Configures security-hardened defaults for production deployment.
    /// Disables DevTools and context menu.
    /// </summary>
    /// <remarks>
    /// This method should be called for production builds to prevent end users from
    /// accessing browser developer tools or context menu items like "Inspect Element".
    /// </remarks>
    public HermesBlazorAppBuilder UseProductionDefaults()
    {
        var existing = _windowConfiguration;
        _windowConfiguration = opts =>
        {
            existing?.Invoke(opts);
            opts.DevToolsEnabled = false;
            opts.ContextMenuEnabled = false;
        };
        return this;
    }

    /// <summary>
    /// Explicitly enables or disables the internal dev server for hot reload.
    /// When null (default), the builder auto-detects by checking for the DOTNET_WATCH environment variable.
    /// </summary>
    public HermesBlazorAppBuilder ForceDevServer(bool enabled)
    {
        _forceDevServer = enabled;
        return this;
    }

    /// <summary>
    /// Builds the application.
    /// </summary>
    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public HermesBlazorApp Build()
    {
        var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        var fallbackProvider = Directory.Exists(wwwrootPath)
            ? new PhysicalFileProvider(wwwrootPath)
            : (IFileProvider)new NullFileProvider();

        var appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
        var fileProvider = _fileProvider ?? StaticWebAssetsFileProvider.Create(appName, fallbackProvider);

        var useDevServer = DevServer.DevServerDetector.ShouldUseDevServer(_forceDevServer);
        DevServer.HermesDevServer? devServer = null;

        var window = new HermesWindow();

        if (_windowConfiguration is not null)
        {
            var options = new HermesWindowOptions();
            _windowConfiguration(options);
            ApplyOptions(window, options);
        }

        var backend = GetBackend(window);
        var syncContext = new HermesSynchronizationContext(backend);
        var dispatcher = new HermesDispatcher(syncContext);

        // Pre-register the app scheme before any code that might trigger backend initialization.
        // On macOS, custom schemes must be registered before Initialize() is called.
        // We register even in dev mode (where the scheme isn't used) to satisfy this constraint.
        if (!OperatingSystem.IsWindows())
        {
            backend.RegisterCustomScheme("app", _ => (null, null));
        }

        // Snapshot the developer's service registrations before we add framework services.
        // These are the only ones we forward to the dev server to avoid conflicts
        // between AddBlazorWebView() and AddRazorComponents().
        var developerServiceCount = _hostBuilder.Services.Count;

        _hostBuilder.Services.AddBlazorWebView();
        _hostBuilder.Services.AddSingleton(window);
        _hostBuilder.Services.AddSingleton(backend);
        _hostBuilder.Services.AddSingleton(syncContext);
        _hostBuilder.Services.AddSingleton(dispatcher);
        _hostBuilder.Services.AddSingleton<IConfiguration>(_hostBuilder.Configuration);
        _hostBuilder.Services.AddSingleton<IHermesPlatformService>(new HermesPlatformService(window));
        _hostBuilder.Services.AddSingleton<IHermesMenuProvider>(new HermesMenuProvider(window.MenuBar));

        string? devBaseUri = null;

        if (useDevServer)
        {
            try
            {
                // Forward the developer's service registrations to the dev server.
                // We skip Microsoft.*/System.* types because the HostApplicationBuilder
                // registers internal hosting descriptors (logging, config, etc.) that
                // conflict with the dev server's own builder. Only app-level types get copied.
                var servicesCopy = new Action<IServiceCollection>(devServices =>
                {
                    for (int i = 0; i < developerServiceCount; i++)
                    {
                        var descriptor = _hostBuilder.Services[i];
                        var ns = descriptor.ServiceType.Namespace;
                        if (ns != null && (ns.StartsWith("Microsoft.", StringComparison.Ordinal)
                            || ns.StartsWith("System.", StringComparison.Ordinal)))
                            continue;
                        devServices.Add(descriptor);
                    }
                    devServices.AddSingleton(window);
                    devServices.AddSingleton(backend);
                    devServices.AddSingleton(syncContext);
                    devServices.AddSingleton(dispatcher);
                    devServices.AddSingleton<IConfiguration>(_hostBuilder.Configuration);
                    devServices.AddSingleton<IHermesPlatformService>(new HermesPlatformService(window));
                    devServices.AddSingleton<IHermesMenuProvider>(new HermesMenuProvider(window.MenuBar));
                });

                // The first registered root component is typically the App component,
                // which MapRazorComponents needs to discover routable pages.
                var rootComponent = RootComponents.GetComponents().FirstOrDefault();
                var rootType = rootComponent.Type
                    ?? throw new InvalidOperationException("No root components registered. Add at least one via RootComponents.Add<App>().");

                devServer = DevServer.HermesDevServer.StartAsync(
                    rootType,
                    servicesCopy,
                    _hostPage,
                    wwwrootPath).GetAwaiter().GetResult();

                devBaseUri = devServer.BaseUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hermes] Dev server failed to start: {ex.Message}");
                Console.WriteLine("[Hermes] Falling back to release mode.");
                devServer = null;
            }
        }

        var serviceProvider = _hostBuilder.Services.BuildServiceProvider();
        var jsComponents = new JSComponentConfigurationStore();

        var webViewManager = new HermesWebViewManager(
            backend,
            serviceProvider,
            dispatcher,
            fileProvider,
            jsComponents,
            _hostPage,
            baseUri: devBaseUri,
            isDevMode: devServer is not null);

        SynchronizationContext.SetSynchronizationContext(syncContext);

        if (!_deferWindowShow)
        {
            window.Show();
        }

        var app = new HermesBlazorApp(serviceProvider, _hostBuilder.Configuration, window, webViewManager, syncContext, _loadingHtml, windowShownDuringBuild: !_deferWindowShow, devServer: devServer);

        foreach (var component in RootComponents.GetComponents())
        {
            app.RootComponents.Add(component.Type, component.Selector, component.Parameters);
        }

        return app;
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

    private static IHermesWindowBackend GetBackend(HermesWindow window) =>
        window.Backend;

    private readonly record struct RootComponentRegistration(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties)] Type Type,
        string Selector,
        IDictionary<string, object?>? Parameters);
}

/// <summary>
/// Collection of root components to be added during app build.
/// </summary>
public sealed class RootComponentCollection
{
    private readonly List<(Type Type, string Selector, IDictionary<string, object?>? Parameters)> _components = new();

    /// <summary>
    /// Adds a root component.
    /// </summary>
    public void Add<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] TComponent>(
        string selector) where TComponent : IComponent
    {
        _components.Add((typeof(TComponent), selector, null));
    }

    /// <summary>
    /// Adds a root component with parameters.
    /// </summary>
    public void Add<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] TComponent>(
        string selector,
        IDictionary<string, object?> parameters) where TComponent : IComponent
    {
        _components.Add((typeof(TComponent), selector, parameters));
    }

    internal IEnumerable<(Type Type, string Selector, IDictionary<string, object?>? Parameters)> GetComponents()
        => _components;
}
