// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace IntegrationTestApp.TestScenarios;

/// <summary>
/// A loopback HTTP and WebSocket server whose page, loaded in a cross-origin iframe, tries every route to the host.
/// </summary>
public sealed class SubframeProbeServer : IAsyncDisposable
{
    private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _connections = [];
    private readonly Task _acceptLoop;

    private SubframeProbeServer(TcpListener listener)
    {
        _listener = listener;
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        PageUrl = new Uri($"http://127.0.0.1:{port}/probe.html");
        _acceptLoop = AcceptLoopAsync(_cts.Token);
    }

    public Uri PageUrl { get; }

    public static SubframeProbeServer Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new SubframeProbeServer(listener);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();

        Task[] connections;
        lock (_connections)
            connections = [_acceptLoop, .. _connections];

        try
        {
            await Task.WhenAll(connections).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or IOException or TimeoutException or WebSocketException)
        {
        }

        _cts.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            lock (_connections)
                _connections.Add(HandleConnectionAsync(client, cancellationToken));
        }
    }

    private static async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        try
        {
            var stream = client.GetStream();
            var (path, headers) = await ReadRequestHeadAsync(stream, cancellationToken);

            if (path == "/ws" && headers.TryGetValue("Sec-WebSocket-Key", out var key))
                await EchoWebSocketAsync(stream, key, cancellationToken);
            else if (path == "/probe.html")
                await RespondAsync(stream, "200 OK", "text/html; charset=utf-8", ProbePage, cancellationToken);
            else if (path == "/data.json")
                await RespondAsync(stream, "200 OK", "application/json", """{"probe":"fetched"}""", cancellationToken);
            else
                await RespondAsync(stream, "404 Not Found", "text/plain", "not found", cancellationToken);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException or WebSocketException or InvalidDataException)
        {
        }
    }

    private static async Task<(string Path, Dictionary<string, string> Headers)> ReadRequestHeadAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        var head = new StringBuilder();
        var buffer = new byte[1];
        while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            // One byte at a time so nothing after the head (a WebSocket frame) is consumed here.
            if (await stream.ReadAsync(buffer, cancellationToken) == 0 || head.Length > 16 * 1024)
                throw new InvalidDataException("Incomplete request head");
            head.Append((char)buffer[0]);
        }

        var lines = head.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var target = lines[0].Split(' ')[1];
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        var query = target.IndexOf('?');
        return (query >= 0 ? target[..query] : target, headers);
    }

    private static async Task RespondAsync(
        NetworkStream stream, string status, string contentType, string body, CancellationToken cancellationToken)
    {
        var content = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nContent-Length: {content.Length}\r\n" +
                   "Cache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken);
        await stream.WriteAsync(content, cancellationToken);
    }

    private static async Task EchoWebSocketAsync(NetworkStream stream, string key, CancellationToken cancellationToken)
    {
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + WebSocketGuid)));
        var head = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                   $"Sec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });

        var buffer = new byte[1024];
        var received = await socket.ReceiveAsync(buffer, timeout.Token);
        if (received.MessageType == WebSocketMessageType.Text)
            await socket.SendAsync(buffer.AsMemory(0, received.Count), WebSocketMessageType.Text, true, timeout.Token);

        while (socket.State == WebSocketState.Open)
        {
            var next = await socket.ReceiveAsync(buffer, timeout.Token);
            if (next.MessageType == WebSocketMessageType.Close)
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
        }
    }

    // Reports to the parent with window.postMessage, which is not a route to the host.
    private const string ProbePage = """
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><title>Subframe probe</title></head>
        <body>
        <p>Subframe probe</p>
        <script>
        (function () {
            var report = { type: 'subframe-report', routes: {} };
            var drag = JSON.stringify({ type: 'hermes-drag', action: 'double-click' });
            var handlers = window.webkit && window.webkit.messageHandlers;

            function attempt(name, send) {
                try { report.routes[name] = send() === false ? 'absent' : 'posted'; }
                catch (e) { report.routes[name] = 'threw:' + (e && e.name); }
            }

            attempt('external', function () {
                if (!window.external || typeof window.external.sendMessage !== 'function') return false;
                window.external.sendMessage('subframe-leak:external');
                window.external.sendMessage(drag);
            });
            attempt('hermesinterop', function () {
                if (!handlers || !handlers.hermesinterop) return false;
                handlers.hermesinterop.postMessage('subframe-leak:hermesinterop');
            });
            attempt('hermesDragRegion', function () {
                if (!handlers || !handlers.hermesDragRegion) return false;
                handlers.hermesDragRegion.postMessage('drag');
                handlers.hermesDragRegion.postMessage('double-click');
            });
            attempt('hermesHost', function () {
                if (!handlers || !handlers.hermesHost) return false;
                handlers.hermesHost.postMessage('subframe-leak:hermesHost');
                handlers.hermesHost.postMessage(drag);
            });
            attempt('chrome.webview', function () {
                var webview = window.chrome && window.chrome.webview;
                if (!webview) return false;
                webview.postMessage('subframe-leak:chrome.webview');
                webview.postMessage(drag);
            });
            attempt('parent.external', function () {
                parent.external.sendMessage('subframe-leak:parent');
            });

            function openSocket() {
                return new Promise(function (resolve) {
                    var settled = false;
                    function done(result) {
                        if (settled) return;
                        settled = true;
                        report.socket = result;
                        resolve();
                    }
                    try {
                        var socket = new WebSocket('ws://' + location.host + '/ws');
                        socket.onopen = function () { socket.send('echo'); };
                        socket.onmessage = function (e) { done(e.data === 'echo' ? 'ok' : 'unexpected:' + e.data); socket.close(); };
                        socket.onerror = function () { done('error'); };
                        setTimeout(function () { done('timeout'); }, 5000);
                    } catch (e) {
                        done('threw:' + e.name);
                    }
                });
            }

            fetch('/data.json')
                .then(function (response) { return response.json(); })
                .then(function (body) { report.fetch = body.probe === 'fetched' ? 'ok' : 'unexpected body'; })
                .catch(function (e) { report.fetch = 'error:' + e; })
                .then(openSocket)
                .then(function () { parent.postMessage(report, '*'); });
        })();
        </script>
        </body>
        </html>
        """;
}
