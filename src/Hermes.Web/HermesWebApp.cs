// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Hosting;
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
    private bool _disposed;

    internal HermesWebApp(IHost host, HermesWindow window, InteropBridge bridge)
    {
        _window = window;
        Bridge = bridge;
        Services = host.Services;
        Configuration = host.Services.GetRequiredService<IConfiguration>();
        _hostRunner = new HermesHostRunner(host, window);
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
        _hostRunner.Stop();
        _window.Dispose();

        await _hostRunner.DisposeAsync();
    }
}
