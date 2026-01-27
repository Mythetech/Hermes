using System.Diagnostics.CodeAnalysis;
using Hermes.Abstractions;
using Hermes.Blazor.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor;

/// <summary>
/// Builder for configuring and creating a Hermes Blazor application.
/// </summary>
public sealed class HermesBlazorAppBuilder
{
    private readonly ServiceCollection _services = new();
    private readonly List<RootComponentRegistration> _rootComponents = new();
    private IFileProvider? _fileProvider;
    private Action<HermesWindowOptions>? _windowConfiguration;
    private string _hostPage = "index.html";

    private HermesBlazorAppBuilder() { }

    /// <summary>
    /// Creates a new builder with default configuration.
    /// </summary>
    public static HermesBlazorAppBuilder CreateDefault(string[]? args = null)
    {
        var builder = new HermesBlazorAppBuilder();

        // Add basic services
        builder._services.AddLogging();

        return builder;
    }

    /// <summary>
    /// Gets the service collection for adding custom services.
    /// </summary>
    public IServiceCollection Services => _services;

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
    /// Builds the application.
    /// </summary>
    [RequiresDynamicCode("Blazor WebView requires dynamic code for component rendering")]
    [RequiresUnreferencedCode("Blazor WebView uses reflection for component instantiation")]
    public HermesBlazorApp Build()
    {
        // Create file provider if not set
        var fileProvider = _fileProvider ?? new PhysicalFileProvider(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot"));

        // Create the window
        var window = new HermesWindow();

        // Apply window configuration
        if (_windowConfiguration is not null)
        {
            var options = new HermesWindowOptions();
            _windowConfiguration(options);
            ApplyOptions(window, options);
        }

        // Get the backend (triggers initialization)
        window.Show();
        var backend = GetBackend(window);

        // Create threading infrastructure
        var syncContext = new HermesSynchronizationContext(backend);
        var dispatcher = new HermesDispatcher(syncContext);

        // Add Blazor services
        _services.AddSingleton(window);
        _services.AddSingleton(backend);
        _services.AddSingleton(syncContext);
        _services.AddSingleton(dispatcher);

        // Build service provider
        var serviceProvider = _services.BuildServiceProvider();

        // Create JS component store
        var jsComponents = new JSComponentConfigurationStore();

        // Create WebView manager
        var webViewManager = new HermesWebViewManager(
            backend,
            serviceProvider,
            dispatcher,
            fileProvider,
            jsComponents,
            _hostPage);

        // Create app
        var app = new HermesBlazorApp(serviceProvider, window, webViewManager, syncContext);

        // Add root components
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

    private static IHermesWindowBackend GetBackend(HermesWindow window)
    {
        // Direct access via internal property - no reflection needed
        return window.Backend;
    }

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
