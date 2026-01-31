using System.Diagnostics.CodeAnalysis;
using Hermes.Blazor.Threading;
using Hermes.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;

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
    private readonly string? _loadingHtml;
    private readonly bool _windowShownDuringBuild;
    private bool _disposed;

    internal HermesBlazorApp(
        IServiceProvider services,
        IConfiguration configuration,
        HermesWindow window,
        HermesWebViewManager webViewManager,
        HermesSynchronizationContext syncContext,
        string? loadingHtml = null,
        bool windowShownDuringBuild = true)
    {
        _services = services;
        _configuration = configuration;
        _window = window;
        _webViewManager = webViewManager;
        _syncContext = syncContext;
        _loadingHtml = loadingHtml;
        _windowShownDuringBuild = windowShownDuringBuild;

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
        SynchronizationContext.SetSynchronizationContext(_syncContext);

        // If window wasn't shown during Build() (deferred show mode), show it now
        if (!_windowShownDuringBuild)
        {
            _window.Show();
        }

        // Fire-and-forget component initialization - don't block waiting for it.
        // The actual component rendering happens after Navigate when Blazor's JS boots.
        // Blocking here would deadlock on Windows because the async continuations
        // need the message loop, but WaitForClose() hasn't started yet.
        _ = RootComponents.InitializeAsync();

        _webViewManager.Navigate("/");
        _window.WaitForClose();
    }

    /// <summary>
    /// Run the application with optimized two-stage startup for faster perceived performance.
    /// Shows the window immediately with loading content, then initializes Blazor in the background.
    /// This method blocks until the window is closed.
    /// </summary>
    /// <remarks>
    /// This approach provides faster perceived startup by showing the window before Blazor
    /// is fully initialized. The window displays a loading state while Blazor components
    /// are being set up, then navigates to the actual content once ready.
    /// </remarks>
    public void RunWithFastStartup()
    {
        SynchronizationContext.SetSynchronizationContext(_syncContext);

        // Phase 1: Show window immediately with loading state (fast - native only)
        _window.ShowWithLoadingState(_loadingHtml);

        // Phase 2: Initialize Blazor components asynchronously (can be slower)
        // This runs on the UI thread via the synchronization context
        _ = InitializeAndNavigateAsync();

        // Phase 3: Enter message loop (required for async continuations)
        _window.WaitForClose();
    }

    private async Task InitializeAndNavigateAsync()
    {
        try
        {
            // Wait a frame to ensure window is fully visible
            await Task.Yield();

            // Initialize root components
            await RootComponents.InitializeAsync();

            // Navigate to actual content, replacing the loading state
            _webViewManager.Navigate("/");
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Blazor initialization failed: {ex}");
            _window.LoadHtml(CreateErrorHtml(ex));
        }
    }

    private static string CreateErrorHtml(Exception ex)
    {
        var errorId = Guid.NewGuid().ToString("N")[..8];
        var details = System.Net.WebUtility.HtmlEncode(ex.ToString());
#if DEBUG
        var detailsOpen = "open";
        var buildNote = " (Debug Build)";
#else
        var detailsOpen = "";
        var buildNote = "";
#endif

        return $@"<!DOCTYPE html>
<html>
<head><style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; padding: 20px; color: #333; }}
    h1 {{ color: #c00; font-size: 1.5em; }}
    .error-id {{ font-size: 0.85em; color: #666; }}
    summary {{ cursor: pointer; color: #0066cc; }}
    pre {{ background: #f5f5f5; padding: 1em; overflow: auto; font-size: 0.85em; border-radius: 4px; }}
</style></head>
<body>
    <h1>Startup Error</h1>
    <p>The application encountered an error during startup.</p>
    <p class=""error-id"">Error ID: {errorId}</p>
    <details {detailsOpen}>
        <summary>Technical Details{buildNote}</summary>
        <pre>{details}</pre>
    </details>
</body>
</html>";
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
            _ = _webViewManager.AddRootComponentAsync(
                componentType,
                selector,
                parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters));
        }
        else
        {
            _pendingComponents.Add(new RootComponentRegistration(componentType, selector, parameters));
        }
    }

    internal async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        foreach (var registration in _pendingComponents)
        {
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
