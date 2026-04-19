// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Net;
using System.Net.Sockets;
using Hermes.Mobile.WebView;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Mobile.iOS.WebView;

/// <summary>
/// Minimal HTTP server that serves static files from an <see cref="IFileProvider"/> on localhost.
/// WKWebView custom scheme handlers are broken on .NET iOS (dotnet/macios regression),
/// so we serve Blazor assets over HTTP and rely on the working WKScriptMessageHandler
/// for the JS↔C# bridge.
/// </summary>
internal sealed class EmbeddedFileServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly IFileProvider _fileProvider;
    private readonly CancellationTokenSource _cts = new();

    public int Port { get; }
    public string BaseUrl => $"http://localhost:{Port}";

    private EmbeddedFileServer(HttpListener listener, IFileProvider fileProvider, int port)
    {
        _listener = listener;
        _fileProvider = fileProvider;
        Port = port;
    }

    internal static EmbeddedFileServer Start(IFileProvider fileProvider)
    {
        var port = FindFreePort();
        var prefix = $"http://localhost:{port}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var server = new EmbeddedFileServer(listener, fileProvider, port);
        _ = Task.Run(() => server.AcceptLoopAsync());

        Console.WriteLine($"[Hermes.Mobile] Embedded file server listening on {prefix}");
        return server;
    }

    private static int FindFreePort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }

            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hermes.Mobile] File server request error: {ex.Message}");
                try { context.Response.StatusCode = 500; context.Response.Close(); }
                catch { /* best effort */ }
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath?.TrimStart('/') ?? "";
        if (string.IsNullOrEmpty(path))
            path = "index.html";

        var fileInfo = _fileProvider.GetFileInfo(path);

        // blazor.modules.json is required by Blazor but may not be in the bundle.
        // Return an empty array to satisfy the framework.
        if (!fileInfo.Exists && path.EndsWith("blazor.modules.json", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            var bytes = System.Text.Encoding.UTF8.GetBytes("[]");
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
            return;
        }

        // SPA fallback: serve index.html for paths that don't resolve to a file
        if (!fileInfo.Exists && !Path.HasExtension(path))
            fileInfo = _fileProvider.GetFileInfo("index.html");

        if (fileInfo.Exists)
        {
            var contentType = MimeTypeLookup.GetContentType(path);
            using var stream = fileInfo.CreateReadStream();
            context.Response.ContentType = contentType;
            context.Response.StatusCode = 200;
            context.Response.Headers.Set("Cache-Control", "no-cache, max-age=0, must-revalidate, no-store");
            stream.CopyTo(context.Response.OutputStream);
        }
        else
        {
            context.Response.StatusCode = 404;
        }

        context.Response.Close();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        _cts.Dispose();
    }
}
