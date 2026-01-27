using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Gtk;
using WebKit;

namespace Hermes.Platforms.Linux;

[SupportedOSPlatform("linux")]
internal sealed class LinuxWindowBackend : IHermesWindowBackend
{
    private static bool s_gtkInitialized;
    private static readonly object s_initLock = new();

    private Gtk.Window _window = null!;
    private WebKit.WebView _webView = null!;
    private WebKit.UserContentManager _userContentManager = null!;
    private VBox _mainContainer = null!;
    private HermesWindowOptions _options = null!;
    private bool _isInitialized;
    private bool _isDisposed;
    private int _uiThreadId;

    private int _lastWidth;
    private int _lastHeight;
    private int _lastX;
    private int _lastY;

    private readonly ConcurrentQueue<(Action Action, TaskCompletionSource Tcs)> _invokeQueue = new();
    private readonly Dictionary<string, Func<string, Stream?>> _customSchemeHandlers = new();

    private LinuxMenuBackend? _menuBackend;

    public event Action? Closing;
    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action? FocusIn;
    public event Action? FocusOut;
    public event Action<string>? WebMessageReceived;

    public void Initialize(HermesWindowOptions options)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Window already initialized");

        _options = options;
        _uiThreadId = Environment.CurrentManagedThreadId;

        EnsureGtkInitialized();

        // Create main window
        _window = new Gtk.Window(Gtk.WindowType.Toplevel);
        _window.Title = options.Title;
        _window.SetDefaultSize(options.Width, options.Height);

        // Position
        if (options.CenterOnScreen)
        {
            _window.SetPosition(Gtk.WindowPosition.Center);
        }
        else if (options.X.HasValue && options.Y.HasValue)
        {
            _window.Move(options.X.Value, options.Y.Value);
        }

        // Chromeless (no decorations)
        if (options.Chromeless)
        {
            _window.Decorated = false;
        }

        // Resizable
        _window.Resizable = options.Resizable;

        // TopMost (keep above)
        if (options.TopMost)
        {
            _window.KeepAbove = true;
        }

        // Size constraints
        if (options.MinWidth.HasValue || options.MinHeight.HasValue ||
            options.MaxWidth.HasValue || options.MaxHeight.HasValue)
        {
            var geometry = new Gdk.Geometry
            {
                MinWidth = options.MinWidth ?? 1,
                MinHeight = options.MinHeight ?? 1,
                MaxWidth = options.MaxWidth ?? int.MaxValue,
                MaxHeight = options.MaxHeight ?? int.MaxValue
            };

            var hints = Gdk.WindowHints.MinSize | Gdk.WindowHints.MaxSize;
            _window.SetGeometryHints(_window, geometry, hints);
        }

        // Icon
        if (!string.IsNullOrEmpty(options.IconPath) && File.Exists(options.IconPath))
        {
            try
            {
                _window.SetIconFromFile(options.IconPath);
            }
            catch
            {
                // Ignore icon load failures
            }
        }

        // Wire up window events
        _window.DeleteEvent += OnWindowDeleteEvent;
        _window.ConfigureEvent += OnWindowConfigureEvent;
        _window.FocusInEvent += OnWindowFocusInEvent;
        _window.FocusOutEvent += OnWindowFocusOutEvent;

        // Create main container
        _mainContainer = new VBox(false, 0);

        // Initialize WebView
        InitializeWebView();

        // Add WebView to container (menu bar will be added above if needed)
        _mainContainer.PackStart(_webView, true, true, 0);

        _window.Add(_mainContainer);

        _isInitialized = true;
    }

    private void InitializeWebView()
    {
        // Create UserContentManager for script message handling
        _userContentManager = new WebKit.UserContentManager();

        // Register script message handler for window.external.sendMessage()
        _userContentManager.RegisterScriptMessageHandler("hermesHost");
        _userContentManager.ScriptMessageReceived += OnScriptMessageReceived;

        // Create WebView with user content manager
        _webView = new WebKit.WebView(_userContentManager);

        // Configure settings
        var settings = _webView.Settings;
        settings.EnableDeveloperExtras = _options.DevToolsEnabled;
        settings.JavascriptCanAccessClipboard = true;
        settings.EnableJavascript = true;

        // Disable context menu if requested
        if (!_options.ContextMenuEnabled)
        {
            _webView.ContextMenu += (sender, args) =>
            {
                args.RetVal = true; // Suppress context menu
            };
        }

        // Register custom URI schemes
        var context = _webView.Context;
        foreach (var scheme in _customSchemeHandlers.Keys)
        {
            RegisterSchemeWithContext(context, scheme);
        }

        // Inject JavaScript bridge at document start
        var bridgeScript = new WebKit.UserScript(
            """
            window.external = {
                sendMessage: function(message) {
                    window.webkit.messageHandlers.hermesHost.postMessage(message);
                },
                receiveMessage: function(callback) {
                    window.__hermesReceiveCallback = callback;
                }
            };
            """,
            WebKit.UserContentInjectedFrames.AllFrames,
            WebKit.UserScriptInjectionTime.Start,
            null, null);
        _userContentManager.AddScript(bridgeScript);
    }

    public void Show()
    {
        ThrowIfNotInitialized();

        if (_options.Maximized)
            _window.Maximize();
        else if (_options.Minimized)
            _window.Iconify();

        _window.ShowAll();

        // Navigate to initial content
        if (!string.IsNullOrEmpty(_options.StartUrl))
            _webView.LoadUri(_options.StartUrl);
        else if (!string.IsNullOrEmpty(_options.StartHtml))
            _webView.LoadHtml(_options.StartHtml, "about:blank");
    }

    public void WaitForClose()
    {
        Show();
        Gtk.Application.Run();
    }

    public void Close()
    {
        if (_isInitialized && _window != null)
        {
            GLib.Idle.Add(() =>
            {
                _window.Destroy();
                Gtk.Application.Quit();
                return false;
            });
        }
    }

    public string Title
    {
        get
        {
            ThrowIfNotInitialized();
            return _window.Title ?? string.Empty;
        }
        set
        {
            ThrowIfNotInitialized();
            _window.Title = value;
        }
    }

    public (int Width, int Height) Size
    {
        get
        {
            ThrowIfNotInitialized();
            _window.GetSize(out int w, out int h);
            return (w, h);
        }
        set
        {
            ThrowIfNotInitialized();
            _window.Resize(value.Width, value.Height);
        }
    }

    public (int X, int Y) Position
    {
        get
        {
            ThrowIfNotInitialized();
            _window.GetPosition(out int x, out int y);
            return (x, y);
        }
        set
        {
            ThrowIfNotInitialized();
            _window.Move(value.X, value.Y);
        }
    }

    public bool IsMaximized
    {
        get
        {
            ThrowIfNotInitialized();
            return _window.IsMaximized;
        }
        set
        {
            ThrowIfNotInitialized();
            if (value)
                _window.Maximize();
            else
                _window.Unmaximize();
        }
    }

    public bool IsMinimized
    {
        get
        {
            ThrowIfNotInitialized();
            // GTK doesn't have a direct IsMinimized property; check window state
            var state = _window.Window?.State ?? Gdk.WindowState.Withdrawn;
            return (state & Gdk.WindowState.Iconified) != 0;
        }
        set
        {
            ThrowIfNotInitialized();
            if (value)
                _window.Iconify();
            else
                _window.Deiconify();
        }
    }

    public void NavigateToUrl(string url)
    {
        ThrowIfNotInitialized();
        _webView.LoadUri(url);
    }

    public void NavigateToString(string html)
    {
        ThrowIfNotInitialized();
        _webView.LoadHtml(html, "about:blank");
    }

    public void SendWebMessage(string message)
    {
        ThrowIfNotInitialized();

        // Escape the message for JavaScript
        var escaped = message
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");

        var script = $"if(window.__hermesReceiveCallback) window.__hermesReceiveCallback('{escaped}');";

        // Use modern EvaluateJavascript API (not deprecated RunJavascript)
        _webView.RunJavascript(script, null, null);
    }

    public void RegisterCustomScheme(string scheme, Func<string, Stream?> handler)
    {
        _customSchemeHandlers[scheme] = handler;

        if (_webView != null)
        {
            RegisterSchemeWithContext(_webView.Context, scheme);
        }
    }

    private void RegisterSchemeWithContext(WebKit.WebContext context, string scheme)
    {
        context.RegisterUriScheme(scheme, (request) =>
        {
            var uri = request.Uri;

            if (_customSchemeHandlers.TryGetValue(scheme, out var handler))
            {
                var stream = handler(uri);
                if (stream != null)
                {
                    try
                    {
                        // Read stream to bytes
                        var bytes = ReadStreamToBytes(stream);
                        var mimeType = GetMimeType(uri);

                        // Create GLib input stream from bytes
                        var inputStream = new GLib.MemoryInputStream();
                        inputStream.AddData(bytes, null);

                        request.Finish(inputStream, bytes.Length, mimeType);
                    }
                    catch
                    {
                        FinishWithError(request);
                    }
                }
                else
                {
                    FinishWithError(request);
                }
            }
            else
            {
                FinishWithError(request);
            }
        });
    }

    private static void FinishWithError(WebKit.URISchemeRequest request)
    {
        var error = new GLib.Error(GLib.Quark.FromString("hermes"), 404, "Not Found");
        request.FinishError(error);
    }

    private static byte[] ReadStreamToBytes(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static string GetMimeType(string uri)
    {
        var path = uri;
        var queryIndex = uri.IndexOf('?');
        if (queryIndex >= 0)
            path = uri[..queryIndex];

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".eot" => "application/vnd.ms-fontobject",
            ".ico" => "image/x-icon",
            ".webp" => "image/webp",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            ".wasm" => "application/wasm",
            _ => "application/octet-stream"
        };
    }

    public int UIThreadId => _uiThreadId;

    public bool CheckAccess() => Environment.CurrentManagedThreadId == _uiThreadId;

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        GLib.Idle.Add(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
            return false; // Remove idle handler after execution
        });

        tcs.Task.GetAwaiter().GetResult();
    }

    public void BeginInvoke(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        // Fire-and-forget
        GLib.Idle.Add(() =>
        {
            try
            {
                action();
            }
            catch
            {
                // Ignore exceptions in fire-and-forget
            }
            return false;
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _webView?.Dispose();
        _userContentManager?.Dispose();
        _mainContainer?.Dispose();
        _window?.Dispose();
    }

    internal LinuxMenuBackend CreateMenuBackend()
    {
        ThrowIfNotInitialized();
        _menuBackend ??= new LinuxMenuBackend(_window, _mainContainer, _webView);
        return _menuBackend;
    }

    internal LinuxDialogBackend CreateDialogBackend()
    {
        ThrowIfNotInitialized();
        return new LinuxDialogBackend(_window);
    }

    internal Gtk.Window Window => _window;

    private static void EnsureGtkInitialized()
    {
        if (s_gtkInitialized) return;

        lock (s_initLock)
        {
            if (s_gtkInitialized) return;

            Gtk.Application.Init();
            s_gtkInitialized = true;
        }
    }

    private void OnWindowDeleteEvent(object? sender, DeleteEventArgs args)
    {
        Closing?.Invoke();
        args.RetVal = false; // Allow close to proceed
    }

    private void OnWindowConfigureEvent(object? sender, ConfigureEventArgs args)
    {
        var evt = args.Event;

        // Fire Resized only when size actually changed
        if (evt.Width != _lastWidth || evt.Height != _lastHeight)
        {
            _lastWidth = evt.Width;
            _lastHeight = evt.Height;
            Resized?.Invoke(evt.Width, evt.Height);
        }

        // Fire Moved only when position actually changed
        if (evt.X != _lastX || evt.Y != _lastY)
        {
            _lastX = evt.X;
            _lastY = evt.Y;
            Moved?.Invoke(evt.X, evt.Y);
        }
    }

    private void OnWindowFocusInEvent(object? sender, FocusInEventArgs args)
    {
        FocusIn?.Invoke();
    }

    private void OnWindowFocusOutEvent(object? sender, FocusOutEventArgs args)
    {
        FocusOut?.Invoke();
    }

    private void OnScriptMessageReceived(object? sender, ScriptMessageReceivedArgs args)
    {
        try
        {
            var jsResult = args.JsResult;
            var value = jsResult.JsValue;

            if (value.IsString)
            {
                var message = value.ToString();
                WebMessageReceived?.Invoke(message);
            }
        }
        catch
        {
            // Ignore JavaScript result parsing errors
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Window not initialized. Call Initialize() first.");
    }
}
