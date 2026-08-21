// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Diagnostics.CodeAnalysis;
using Hermes.Blazor.Threading;
using Hermes.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Hermes.Blazor.DevServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ApplicationLifetime = Microsoft.Extensions.Hosting.Internal.ApplicationLifetime;

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
    private readonly HermesDevServer? _devServer;
    private readonly IHost? _host;
    private readonly IHostApplicationLifetime? _applicationLifetime;
    private readonly CancellationTokenSource _hostStartCancellation = new();
    private Task? _hostStartTask;

    internal HermesBlazorApp(
        IServiceProvider services,
        IConfiguration configuration,
        HermesWindow window,
        HermesWebViewManager webViewManager,
        HermesSynchronizationContext syncContext,
        string? loadingHtml = null,
        bool windowShownDuringBuild = true,
        HermesDevServer? devServer = null,
        IHost? host = null)
    {
        _services = services;
        _configuration = configuration;
        _window = window;
        _webViewManager = webViewManager;
        _syncContext = syncContext;
        _loadingHtml = loadingHtml;
        _windowShownDuringBuild = windowShownDuringBuild;
        _devServer = devServer;
        _host = host;
        _applicationLifetime = host?.Services.GetService<IHostApplicationLifetime>();

        if (_applicationLifetime is not null)
        {
            // ApplicationStopping ties to the beginning of window close, not to
            // disposal, so services can react while the window still exists.
            _window.Backend.Closing += NotifyApplicationStopping;
        }

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

    private Task? _initializationTask;

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

        // Component initialization rides the message loop: its continuations post
        // through the synchronization context into the native queue, which drains
        // once WaitForClose starts pumping. Blocking here instead would deadlock
        // on Windows because the WebView2 continuations need the pump. The task
        // is observed in DisposeAsync, so this is not fire-and-forget.
        _initializationTask = RootComponents.InitializeAsync();

        // Navigate synchronously before entering the loop: issuing the native
        // load request now lets the WebView kick off its content process spawn
        // while the loop is still starting. Deferring this into the loop was
        // measured about 65ms slower to first render on macOS.
        _webViewManager.Navigate("/");

        BeginHostStart();

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
        // This runs on the UI thread via the synchronization context.
        // The task is observed in DisposeAsync.
        _initializationTask = InitializeAndNavigateAsync();

        BeginHostStart();

        // Phase 3: Enter message loop (required for async continuations)
        _window.WaitForClose();
    }

    private void BeginHostStart()
    {
        if (_host is null || _hostStartTask is not null)
            return;

        // Hosted services start on the thread pool so a slow StartAsync can
        // never delay first paint or the message loop. The task observes its
        // own exceptions and is joined in DisposeAsync, so it is not
        // fire-and-forget.
        _hostStartTask = Task.Run(() => StartHostAsync(_hostStartCancellation.Token));
    }

    private async Task StartHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The window is already visible, which is the honest "started"
            // signal for a desktop app. Fire ApplicationStarted now instead of
            // after every hosted service finishes starting; the host's own
            // NotifyStarted call later is an idempotent no-op.
            if (_applicationLifetime is ApplicationLifetime applicationLifetime)
                applicationLifetime.NotifyStarted();

            await _host!.StartAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown began before hosted services finished starting.
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Hosted services failed to start: {ex}");
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private void NotifyApplicationStopping() => _applicationLifetime!.StopApplication();

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

        if (_initializationTask is not null)
        {
            // Observe the initialization task so faults are never lost. If the
            // message loop exited before the posted continuations ran, the task
            // can never complete, so cap the wait instead of hanging dispose.
            try
            {
                var completed = await Task.WhenAny(_initializationTask, Task.Delay(TimeSpan.FromSeconds(2)));
                if (completed == _initializationTask)
                    await _initializationTask;
            }
            catch (Exception ex)
            {
                HermesLogger.Error($"Startup initialization task faulted: {ex}");
            }
        }

        if (_host is not null)
            StopHost();

        await _webViewManager.DisposeAsync();
        _window.Dispose();

        if (_devServer is not null)
            await _devServer.DisposeAsync();

        if (_host is not null)
        {
            // Disposing the host also disposes the service provider it built.
            if (_host is IAsyncDisposable hostAsyncDisposable)
                await hostAsyncDisposable.DisposeAsync();
            else
                _host.Dispose();
        }
        else if (_services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_services is IDisposable disposable)
            disposable.Dispose();

        _hostStartCancellation.Dispose();
    }

    private void StopHost()
    {
        if (_applicationLifetime is not null)
            _window.Backend.Closing -= NotifyApplicationStopping;

        _hostStartCancellation.Cancel();

        // Blocking joins, deliberately: the message loop has already exited by
        // the time the app is disposed, so await continuations posted through
        // the synchronization context would land in a queue nothing drains.
        // The start task runs entirely on the thread pool and observes its own
        // exceptions, so these waits complete without the pump.
        if (_hostStartTask is not null && !_hostStartTask.Wait(ShutdownTimeout))
        {
            HermesLogger.Error(
                "Hosted services did not finish starting within the shutdown timeout; continuing teardown.");
        }

        try
        {
            _host!.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            HermesLogger.Error($"Hosted services failed to stop cleanly: {ex}");
        }
    }

    private TimeSpan ShutdownTimeout =>
        _host!.Services.GetService<IOptions<HostOptions>>()?.Value.ShutdownTimeout
            ?? TimeSpan.FromSeconds(5);
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
