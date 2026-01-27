using System.Diagnostics.CodeAnalysis;
using Hermes.Abstractions;
using Hermes.Blazor.Diagnostics;
using Hermes.Blazor.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor;

/// <summary>
/// Main Blazor desktop application class for Hermes.
/// </summary>
public sealed class HermesBlazorApp : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly HermesWindow _window;
    private readonly HermesWebViewManager _webViewManager;
    private readonly HermesSynchronizationContext _syncContext;
    private bool _disposed;

    internal HermesBlazorApp(
        IServiceProvider services,
        IConfiguration configuration,
        HermesWindow window,
        HermesWebViewManager webViewManager,
        HermesSynchronizationContext syncContext)
    {
        _services = services;
        _configuration = configuration;
        _window = window;
        _webViewManager = webViewManager;
        _syncContext = syncContext;

        RootComponents = new HermesRootComponents(_webViewManager);
    }

    /// <summary>
    /// Gets the main window for this application.
    /// </summary>
    public HermesWindow MainWindow => _window;

    /// <summary>
    /// Gets the root components collection for adding Blazor components.
    /// </summary>
    public HermesRootComponents RootComponents { get; }

    /// <summary>
    /// Gets the service provider for this application.
    /// </summary>
    public IServiceProvider Services => _services;

    /// <summary>
    /// Gets the configuration for this application.
    /// </summary>
    public IConfiguration Configuration => _configuration;

    /// <summary>
    /// Run the application. This method blocks until the window is closed.
    /// </summary>
    public void Run()
    {
        StartupLog.Log("Blazor", "Installing SynchronizationContext");
        // Install synchronization context
        SynchronizationContext.SetSynchronizationContext(_syncContext);

        StartupLog.Log("Blazor", "Initializing root components...");
        // Initialize pending root components (adds them to WebViewManager)
        RootComponents.InitializeAsync().GetAwaiter().GetResult();
        StartupLog.Log("Blazor", "Root components initialized");

        // Navigate to the root URL
        StartupLog.Log("Blazor", "Navigating to /");
        _webViewManager.Navigate("/");

        StartupLog.Log("Blazor", "Entering message loop (waiting for close)");
        // Run the window message loop (blocking)
        _window.WaitForClose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _webViewManager.DisposeAsync();
        _window.Dispose();

        if (_services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_services is IDisposable disposable)
            disposable.Dispose();
    }
}

/// <summary>
/// Collection of root Blazor components.
/// </summary>
public sealed class HermesRootComponents
{
    private readonly HermesWebViewManager _webViewManager;
    private readonly List<RootComponentRegistration> _pendingComponents = new();
    private bool _initialized;

    internal HermesRootComponents(HermesWebViewManager webViewManager)
    {
        _webViewManager = webViewManager;
    }

    /// <summary>
    /// Adds a root component to be rendered in the specified selector.
    /// </summary>
    public void Add<[DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties)] TComponent>(
        string selector,
        IDictionary<string, object?>? parameters = null) where TComponent : IComponent
    {
        Add(typeof(TComponent), selector, parameters);
    }

    /// <summary>
    /// Adds a root component to be rendered in the specified selector.
    /// </summary>
    public void Add(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties)] Type componentType,
        string selector,
        IDictionary<string, object?>? parameters = null)
    {
        if (_initialized)
        {
            // Add immediately if already initialized
            _ = _webViewManager.AddRootComponentAsync(
                componentType,
                selector,
                parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters));
        }
        else
        {
            // Queue for later
            _pendingComponents.Add(new RootComponentRegistration(componentType, selector, parameters));
        }
    }

    internal async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        StartupLog.Log("Blazor", $"Adding {_pendingComponents.Count} root component(s) to WebViewManager");
        foreach (var registration in _pendingComponents)
        {
            StartupLog.Log("Blazor", $"  - {registration.ComponentType.Name} -> {registration.Selector}");
            await _webViewManager.AddRootComponentAsync(
                registration.ComponentType,
                registration.Selector,
                registration.Parameters is null
                    ? ParameterView.Empty
                    : ParameterView.FromDictionary(registration.Parameters));
        }

        _pendingComponents.Clear();
    }

    private readonly record struct RootComponentRegistration(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors |
            DynamicallyAccessedMemberTypes.PublicProperties)] Type ComponentType,
        string Selector,
        IDictionary<string, object?>? Parameters);
}
