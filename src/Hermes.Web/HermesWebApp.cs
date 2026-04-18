// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Web.Interop;

namespace Hermes.Web;

public sealed class HermesWebApp : IDisposable
{
    private readonly HermesWindow _window;
    private bool _disposed;

    internal HermesWebApp(HermesWindow window, InteropBridge? bridge)
    {
        _window = window;
        Bridge = bridge;
    }

    public HermesWindow MainWindow => _window;

    public InteropBridge? Bridge { get; }

    public void Run()
    {
        _window.WaitForClose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Bridge?.Detach();
        _window.Dispose();
    }
}
