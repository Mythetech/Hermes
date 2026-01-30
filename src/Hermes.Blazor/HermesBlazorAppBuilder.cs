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

        _hostBuilder.Services.AddBlazorWebView();
        _hostBuilder.Services.AddSingleton(window);
        _hostBuilder.Services.AddSingleton(backend);
        _hostBuilder.Services.AddSingleton(syncContext);
        _hostBuilder.Services.AddSingleton(dispatcher);
        _hostBuilder.Services.AddSingleton<IConfiguration>(_hostBuilder.Configuration);

        var serviceProvider = _hostBuilder.Services.BuildServiceProvider();
        var jsComponents = new JSComponentConfigurationStore();

        var webViewManager = new HermesWebViewManager(
            backend,
            serviceProvider,
            dispatcher,
            fileProvider,
            jsComponents,
            _hostPage);

        // Set sync context before Show() so WebView2 initialization continuations
        // are marshaled back to the UI thread via the message loop
        SynchronizationContext.SetSynchronizationContext(syncContext);

        // For fast startup mode, defer showing until Run/RunWithFastStartup is called
        if (!_deferWindowShow)
        {
            window.Show();
        }

        var app = new HermesBlazorApp(serviceProvider, _hostBuilder.Configuration, window, webViewManager, syncContext, _loadingHtml, windowShownDuringBuild: !_deferWindowShow);

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
