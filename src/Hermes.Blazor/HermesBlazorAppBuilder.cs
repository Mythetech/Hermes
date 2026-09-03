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
using Hermes.Blazor.Diagnostics;
using Hermes.Blazor.Startup;

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

        // The window loop owns process lifetime; the default ConsoleLifetime
        // would install console signal handling that fights the native loop.
        _hostBuilder.Services.AddSingleton<IHostLifetime, WindowHostLifetime>();

        // Hosted services start after first paint, so one slow StartAsync must
        // not serialize the rest, and window close should not hang on a long
        // shutdown. Both stay user-overridable via Configure<HostOptions>.
        _hostBuilder.Services.Configure<HostOptions>(options =>
        {
            options.ServicesStartConcurrently = true;
            options.ShutdownTimeout = TimeSpan.FromSeconds(5);
        });
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
    public HermesBlazorApp Build() => BuildCore(new HermesWindow());

    /// <summary>
    /// Runs the real Build() sequence against a supplied backend so tests can
    /// assert the order of native calls made during startup.
    /// </summary>
    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    internal static HermesBlazorApp BuildForTest(HermesBlazorAppBuilder builder, IHermesWindowBackend backend) =>
        builder.BuildCore(new HermesWindow(backend));

    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    private HermesBlazorApp BuildCore(HermesWindow window)
    {
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

        // The static web assets manifest read costs about 11ms cold on the UI
        // thread (measured 2026-09-03), so it runs on the composition worker as it
        // did before the early handler existed. The early handler resolves the
        // provider only when a request arrives, which in the default path happens
        // only on Linux, and the manager receives the instance after the join.
        var fileProviderHandle = CreateFileProviderHandle(this);

        // Custom schemes must be registered by name before Initialize() on macOS and
        // Linux, and on Windows before the WebView2 controller finishes initializing
        // so its resource filter is in place. The startup handler serves the host
        // page and static assets straight from the file provider until the
        // WebViewManager exists, then forwards to it. It never blocks, which is what
        // lets the UI thread pump native events while composition runs. On Windows
        // the app scheme is http, and registering it during hot reload would install
        // a WebView2 http://* resource filter that intercepts the dev server's own
        // requests, so Windows registers nothing in that mode; on macOS and Linux the
        // app scheme can only be registered before Initialize(), and when the dev
        // server fails to start, ComposeServices falls back to release mode, so the
        // handler must already be registered for the manager to install itself into
        // (this is today's behavior with the deferred handler).
        StartupSchemeHandler? startupHandler = null;
        InlinedHostPage? hostPage = null;
        if (!useDevServer || !OperatingSystem.IsWindows())
        {
            hostPage = new InlinedHostPage(fileProviderHandle, _hostPage);
            startupHandler = new StartupSchemeHandler(new EarlyStaticContentHandler(fileProviderHandle, hostPage));
            backend.RegisterCustomScheme(new Uri(HermesWebViewManager.AppBaseUri).Scheme, startupHandler.Handle);
        }

        // The page script can send its first IPC message while composition is still
        // running; hold those messages and hand them to the manager in order.
        using var messageBuffer = new WebMessageBuffer(backend);

        // Managed composition runs on a worker while this (UI) thread pays for
        // native application and window initialization, then pumps the native
        // loop until the worker finishes. The worker touches no native state and
        // never posts to the UI synchronization context, so pumping cannot
        // deadlock on it.
        var compositionTask = Task.Run(() => ComposeServices(
            window, backend, syncContext, dispatcher, useDevServer, _hostPage,
            fileProviderHandle, _hostBuilder));

        // The synchronization context must be installed before Show(). On
        // Windows, Show() starts async WebView2 initialization whose await
        // continuations capture the current context; without it they resume on
        // thread pool (MTA) threads and every controller call fails COM
        // apartment marshaling (ICoreWebView2Controller QueryInterface error).
        SynchronizationContext.SetSynchronizationContext(syncContext);

        backend.InitializeApplication();
        StartupLog.Phase("app-initialized");

        var navigatedDuringBuild = false;
        if (!_deferWindowShow)
        {
            window.EnsureInitialized();
            StartupLog.Phase("window-initialized");

            // Linux only: issue the initial load before the window is shown.
            // WebKitGTK stalls the UI process for a full 500ms timeout when the
            // WebView is size-allocated before its first load request: the drawing
            // area waits synchronously for a web process that has not been
            // initialized yet, which is what put Linux 400ms behind Photino in the
            // benchmarks. The startup handler answers the early request without
            // the WebViewManager. On macOS, WKWebView does its process launch work
            // synchronously inside the load call: loading before Show() delays the
            // window (CI measured +57ms window-visible), and loading right after
            // Show() inside Build() moves 31 to 45ms of that work into Build() with
            // no first-render gain (measured locally, 2026-09-03), so macOS and
            // Windows keep navigating in Run(). The dev server's base URI is only
            // known after composition, so hot reload keeps navigating in Run() too.
            if (!useDevServer && backend.Platform == HermesPlatform.Linux)
            {
                backend.NavigateToUrl(HermesWebViewManager.AppBaseUri);
                StartupLog.Phase("navigate");
                navigatedDuringBuild = true;
            }

            window.Show();
            StartupLog.Phase("window-shown");
        }

        // Pump native events while the worker composes only when native WebView
        // work is in flight: on Linux the load was issued above, and on Windows
        // Show() started WebView2 initialization whose continuations need the
        // message queue. Otherwise (macOS, where the load is issued in Run(), and
        // any platform with the window deferred) block on the join as before.
        // Pumping with nothing to advance only services AppKit display timers,
        // which on the 3-core macOS CI runner measured about 170ms later
        // window-visible and 210ms later first render (2026-09-03). The
        // startup scheme handler and message buffer above are what make
        // pumping safe before the manager exists.
        var nativeWorkInFlight = navigatedDuringBuild
            || (!_deferWindowShow && backend.Platform == HermesPlatform.Windows);
        if (nativeWorkInFlight)
            PumpUntilComplete(backend, compositionTask);
        var composition = compositionTask.GetAwaiter().GetResult();
        StartupLog.Phase("composition-done");

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
            startupHandler: startupHandler,
            hostPage: hostPage);

        // The manager subscribed in its constructor and nothing pumps between that
        // and this drain, so no message is delivered twice or out of order.
        messageBuffer.Drain(webViewManager.ReplayMessage);
        StartupLog.Phase("manager-ready");

        var app = new HermesBlazorApp(composition.ServiceProvider, _hostBuilder.Configuration, window, webViewManager, syncContext, _loadingHtml, windowShownDuringBuild: !_deferWindowShow, devServer: composition.DevServer, host: composition.Host, navigatedDuringBuild: navigatedDuringBuild);

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
        Lazy<IFileProvider> fileProviderHandle,
        HostApplicationBuilder hostBuilder)
    {
        // Forcing it here, on the worker, keeps the manifest read off the UI
        // thread whenever the worker reaches this point before a request does.
        var resolvedFileProvider = fileProviderHandle.Value;

        var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

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

        // Build the real IHost rather than a bare provider so AddHostedService
        // registrations actually start and stop. Same container, same
        // registrations; the host is started after first paint from the run path.
        var host = hostBuilder.Build();
        var serviceProvider = host.Services;

        // Still on the worker thread, and the WebView is spawning its content
        // process concurrently: spend the wait pre-JITting the renderer stack
        // so the first real render after attach is cheap.
        RendererWarmup.Run(serviceProvider);

        return new BuildComposition(host, serviceProvider, resolvedFileProvider, devServer, devBaseUri);
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
            fileProviderHandle: CreateFileProviderHandle(builder),
            hostBuilder: builder._hostBuilder);
    }

    internal sealed record BuildComposition(
        IHost Host,
        IServiceProvider ServiceProvider,
        IFileProvider FileProvider,
        DevServer.HermesDevServer? DevServer,
        string? DevBaseUri);

    private static IFileProvider CreateDefaultFileProvider()
    {
        var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
        var fallbackProvider = Directory.Exists(wwwrootPath)
            ? new PhysicalFileProvider(wwwrootPath)
            : (IFileProvider)new NullFileProvider();

        var appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "App";
        return StaticWebAssetsFileProvider.Create(appName, fallbackProvider);
    }

    // ExecutionAndPublication is shared by both callers because on Linux the
    // early request can arrive on the UI thread while the worker is already
    // inside the factory; that mode makes the UI thread wait for the single
    // in-flight instance instead of building a second one.
    private static Lazy<IFileProvider> CreateFileProviderHandle(HermesBlazorAppBuilder builder) =>
        new Lazy<IFileProvider>(
            () => builder._fileProvider ?? CreateDefaultFileProvider(),
            LazyThreadSafetyMode.ExecutionAndPublication);

    private static void ApplyOptions(HermesWindow window, HermesWindowOptions options) =>
        HermesWindowOptions.ApplyTo(window, options);

    // Short slices keep the join latency after composition completes negligible
    // while still letting each iteration drain a burst of native work.
    private const int CompositionPumpSliceMilliseconds = 5;

    private static void PumpUntilComplete(IHermesWindowBackend backend, Task composition)
    {
        while (!composition.IsCompleted)
            backend.RunEventLoopIteration(CompositionPumpSliceMilliseconds);
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
