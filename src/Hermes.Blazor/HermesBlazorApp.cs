using System.Diagnostics.CodeAnalysis;
using Hermes.Abstractions;
using Hermes.Blazor.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor;

/// <summary>
/// Main Blazor desktop application class for Hermes.
/// </summary>
public sealed class HermesBlazorApp : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly HermesWindow _window;
    private readonly HermesWebViewManager _webViewManager;
    private readonly HermesSynchronizationContext _syncContext;
    private bool _disposed;

    internal HermesBlazorApp(
        IServiceProvider services,
        HermesWindow window,
        HermesWebViewManager webViewManager,
        HermesSynchronizationContext syncContext)
    {
        _services = services;
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
    /// Run the application. This method blocks until the window is closed.
    /// </summary>
    public void Run()
    {
        // Install synchronization context
        SynchronizationContext.SetSynchronizationContext(_syncContext);

        // Navigate to the root URL
        _webViewManager.Navigate("/");

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
