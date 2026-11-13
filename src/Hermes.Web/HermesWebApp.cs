// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Hermes.Hosting;
using Hermes.Web.Diagnostics;
using Hermes.Web.Interop;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hermes.Web;

/// <summary>
/// A Hermes app whose page is a JavaScript or TypeScript frontend.
/// </summary>
public sealed class HermesWebApp : IAsyncDisposable
{
    private readonly HermesWindow _window;
    private readonly HermesHostRunner _hostRunner;
    private readonly WebSmokeRuntime? _smoke;
    private bool _disposed;

    internal HermesWebApp(IHost host, HermesWindow window, InteropBridge bridge, WebSmokeRuntime? smoke = null)
    {
        _window = window;
        Bridge = bridge;
        Services = host.Services;
        Configuration = host.Services.GetRequiredService<IConfiguration>();
        _hostRunner = new HermesHostRunner(host, window);
        _smoke = smoke;

        Bridge.PageReady += OnPageReady;
        Bridge.PageErrorReported += OnPageError;
    }

    /// <summary>Gets the main window.</summary>
    public HermesWindow MainWindow => _window;

    /// <summary>
    /// The bridge to the page. Register handlers on it after <see cref="HermesWebAppBuilder.Build"/> and before <see cref="Run"/>.
    /// </summary>
    public InteropBridge Bridge { get; }

    /// <summary>Gets the app's services.</summary>
    public IServiceProvider Services { get; }

    /// <summary>Gets the app's configuration.</summary>
    public IConfiguration Configuration { get; }

    /// <summary>
    /// Shows the window, starts hosted services on the thread pool, and blocks until the window closes.
    /// </summary>
    public void Run()
    {
        Bridge.Start();
        _window.Show();
        _hostRunner.BeginStart();
        _window.WaitForClose();
    }

    /// <summary>
    /// Stops the bridge and hosted services, destroys the window, then disposes the app's services.
    /// Call it on the thread that called <see cref="Run"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Blocking joins up to the window's destruction: the loop has ended, so nothing would resume an await on this thread.
        Bridge.Stop();
        _smoke?.WaitForVerdict(_hostRunner.ShutdownTimeout);
        _hostRunner.Stop();
        _window.Dispose();

        await _hostRunner.DisposeAsync();
    }

    private void OnPageReady() => _smoke?.OnPageReady(Services);

    private void OnPageError(PageError error)
    {
        if (_smoke is not null)
            _smoke.OnPageError(error);
        else
            HermesLogger.Error($"The page reported an uncaught {error.Source}: {error.Name}: {error.Message}");
    }
}
