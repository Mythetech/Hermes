// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using Hermes.Abstractions;
using Hermes.Blazor.Threading;
using Hermes.Contracts.Plugins;
using Hermes.Plugins;
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

        var useDevServer = DevServer.DevServerDetector.ShouldUseDevServer(_forceDevServer);

        // Custom schemes must be registered by name before Initialize() on macOS and
        // Linux. The deferred handler lets the window show and the WebView start up
        // before the WebViewManager exists; any request that races the manager blocks
        // inside Handle() until SetInner() is called. Windows resolves handlers per
        // request from a dictionary (see WindowsWindowBackend.RegisterCustomScheme),
        // so the manager registers directly there and no deferred handler is needed.
        DeferredSchemeHandler? deferredHandler = null;
        if (!OperatingSystem.IsWindows())
        {
            deferredHandler = new DeferredSchemeHandler(TimeSpan.FromSeconds(5));
            backend.RegisterCustomScheme("app", deferredHandler.Handle);
        }

        // Managed composition runs on a worker while this (UI) thread pays for
        // native application and window initialization. The worker touches no
        // native state and no synchronization context is installed yet, so the
        // blocking join below cannot deadlock.
        var compositionTask = Task.Run(() => ComposeServices(
            window, backend, syncContext, dispatcher, useDevServer, _hostPage,
            _fileProvider, _hostBuilder));

        backend.InitializeApplication();

        if (!_deferWindowShow)
        {
            window.Show();
        }

        var composition = compositionTask.GetAwaiter().GetResult();

        var jsComponents = new JSComponentConfigurationStore();

        var webViewManager = new HermesWebViewManager(
            backend,
            composition.ServiceProvider,
            dispatcher,
            composition.FileProvider,
            jsComponents,
            _hostPage,
            baseUri: composition.DevBaseUri,
            isDevMode: composition.DevServer is not null,
            deferredHandler: deferredHandler);

        SynchronizationContext.SetSynchronizationContext(syncContext);

        var app = new HermesBlazorApp(composition.ServiceProvider, _hostBuilder.Configuration, window, webViewManager, syncContext, _loadingHtml, windowShownDuringBuild: !_deferWindowShow, devServer: composition.DevServer);

        foreach (var component in RootComponents.GetComponents())
        {
            app.RootComponents.Add(component.Type, component.Selector, component.Parameters);
        }

        return app;
    }

    private static BuildComposition ComposeServices(
        HermesWindow window,
        IHermesWindowBackend backend,
        HermesSynchronizationContext syncContext,
        HermesDispatcher dispatcher,
        bool useDevServer,
        string hostPage,
        IFileProvider? explicitFileProvider,
        HostApplicationBuilder hostBuilder)
    {
        var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        var fallbackProvider = Directory.Exists(wwwrootPath)
            ? new PhysicalFileProvider(wwwrootPath)
            : (IFileProvider)new NullFileProvider();

        var appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
        var fileProvider = explicitFileProvider ?? StaticWebAssetsFileProvider.Create(appName, fallbackProvider);

        DevServer.HermesDevServer? devServer = null;
        string? devBaseUri = null;

        if (useDevServer)
        {
            try
            {
                devServer = DevServer.HermesDevServer.StartAsync(
                    hostPage,
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

        hostBuilder.Services.AddBlazorWebView();
        hostBuilder.Services.AddSingleton(window);
        hostBuilder.Services.AddSingleton(backend);
        hostBuilder.Services.AddSingleton(syncContext);
        hostBuilder.Services.AddSingleton(dispatcher);
        hostBuilder.Services.AddSingleton<IConfiguration>(hostBuilder.Configuration);
        hostBuilder.Services.AddSingleton<IHermesPlatformService>(new HermesPlatformService(window));
        hostBuilder.Services.AddSingleton<IHermesMenuProvider>(new HermesMenuProvider(() => window.MenuBar));
        hostBuilder.Services.AddSingleton<IClipboard, DesktopClipboard>();

        var serviceProvider = hostBuilder.Services.BuildServiceProvider();

        return new BuildComposition(serviceProvider, fileProvider, devServer, devBaseUri);
    }

    internal static BuildComposition ComposeForTest(HermesBlazorAppBuilder builder, IHermesWindowBackend backend)
    {
        var window = new HermesWindow(backend);
        var syncContext = new HermesSynchronizationContext(backend);
        var dispatcher = new HermesDispatcher(syncContext);

        return ComposeServices(
            window, backend, syncContext, dispatcher,
            useDevServer: false,
            hostPage: builder._hostPage,
            explicitFileProvider: builder._fileProvider,
            hostBuilder: builder._hostBuilder);
    }

    internal sealed record BuildComposition(
        IServiceProvider ServiceProvider,
        IFileProvider FileProvider,
        DevServer.HermesDevServer? DevServer,
        string? DevBaseUri);

    private static void ApplyOptions(HermesWindow window, HermesWindowOptions options) =>
        HermesWindowOptions.ApplyTo(window, options);

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
