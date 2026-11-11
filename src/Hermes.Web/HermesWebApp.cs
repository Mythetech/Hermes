// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Web.Interop;

namespace Hermes.Web;

public sealed class HermesWebApp : IDisposable
{
    private readonly HermesWindow _window;
    private bool _disposed;

    internal HermesWebApp(HermesWindow window, InteropBridge bridge)
    {
        _window = window;
        Bridge = bridge;
    }

    public HermesWindow MainWindow => _window;

    /// <summary>
    /// The bridge to the page. Register handlers on it after <see cref="HermesWebAppBuilder.Build"/> and before <see cref="Run"/>.
    /// </summary>
    public InteropBridge Bridge { get; }

    public void Run()
    {
        Bridge.Start();
        _window.WaitForClose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Bridge.Stop();
        _window.Dispose();
    }
}
