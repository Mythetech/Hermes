using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Hermes.Abstractions;
using Hermes.Diagnostics;
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

    private readonly ConcurrentQueue<(System.Action Action, TaskCompletionSource Tcs)> _invokeQueue = new();
    private readonly Dictionary<string, Func<string, Stream?>> _customSchemeHandlers = new();
    private readonly HashSet<string> _registeredSchemes = new();
    // Store callbacks to prevent GC - WebKit calls these from native code
    private readonly List<WebKit.URISchemeRequestCallback> _schemeCallbacks = new();

    private LinuxMenuBackend? _menuBackend;

    public event System.Action? Closing;
    public event System.Action<int, int>? Resized;
    public event System.Action<int, int>? Moved;
    public event System.Action? FocusIn;
    public event System.Action? FocusOut;
    public event System.Action<string>? WebMessageReceived;

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
            catch (Exception ex)
            {
                HermesLogger.Warning($"Failed to load window icon from '{options.IconPath}': {ex.Message}");
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

        // Register custom URI schemes with the WebView's context
        // Must happen after WebView creation but before any navigation
        var context = _webView.Context;
        foreach (var scheme in _customSchemeHandlers.Keys)
        {
            RegisterSchemeWithContext(context, scheme);
        }

        // Configure settings
        var settings = _webView.Settings;
        settings.EnableDeveloperExtras = _options.DevToolsEnabled;
        settings.JavascriptCanAccessClipboard = true;
        settings.EnableJavascript = true;

        // Add load state tracking for diagnostics
        _webView.LoadChanged += (sender, args) =>
        {
            Console.WriteLine($"[Hermes] WebView LoadChanged: {args.LoadEvent} - URI: {_webView.Uri}");
            Console.Out.Flush();
        };

        _webView.LoadFailed += (sender, args) =>
        {
            Console.WriteLine($"[Hermes] WebView LoadFailed: {args.FailingUri} - Error: {args.Error?.Message ?? "unknown"}");
            Console.Out.Flush();
        };

        // Disable context menu if requested
        if (!_options.ContextMenuEnabled)
        {
            _webView.ContextMenu += (sender, args) =>
            {
                args.RetVal = true; // Suppress context menu
            };
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
            null,
            null);
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
        Console.WriteLine($"[Hermes] NavigateToUrl: {url}");
        Console.Out.Flush();
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

        // Use JSON serialization for consistent escaping across platforms
        var json = JsonSerializer.Serialize(message);
        var script = $"if(window.__hermesReceiveCallback) window.__hermesReceiveCallback({json});";

        // Run the JavaScript in the web view (fire-and-forget, no callback needed)
        _webView.RunJavascript(script, null, null);
    }

    public void RegisterCustomScheme(string scheme, Func<string, Stream?> handler)
    {
        _customSchemeHandlers[scheme] = handler;

        // If WebView exists, register with its context immediately
        if (_webView != null)
        {
            RegisterSchemeWithContext(_webView.Context, scheme);
        }
    }

    private void RegisterSchemeWithContext(WebKit.WebContext context, string scheme)
    {
        // Avoid registering the same scheme twice - WebKit doesn't allow it
        if (!_registeredSchemes.Add(scheme))
        {
            Console.WriteLine($"[Hermes] URI scheme '{scheme}' already registered, skipping");
            return;
        }

        // Register scheme with security manager to allow access to local resources
        // Required for WebKit2GTK >= 2.32 for custom schemes to work properly with Blazor
        var securityManager = context.SecurityManager;
        securityManager.RegisterUriSchemeAsLocal(scheme);
        securityManager.RegisterUriSchemeAsSecure(scheme);
        securityManager.RegisterUriSchemeAsCorsEnabled(scheme);

        Console.WriteLine($"[Hermes] Registering URI scheme '{scheme}' with WebContext (SecurityManager configured)");

        // Create and store the callback to prevent garbage collection
        // WebKit calls this from native code, so the delegate must stay alive
        WebKit.URISchemeRequestCallback callback = (request) =>
        {
            var uri = request.Uri;
            Console.WriteLine($"[Hermes] URI scheme handler called for: {uri}");
            Console.Out.Flush();

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

                        Console.WriteLine($"[Hermes] Serving {uri} ({bytes.Length} bytes, {mimeType})");

                        // Create GLib input stream from bytes
                        // Allocate unmanaged memory and copy bytes (GLib takes ownership)
                        var ptr = Marshal.AllocHGlobal(bytes.Length);
                        Marshal.Copy(bytes, 0, ptr, bytes.Length);

                        var inputStream = new GLib.MemoryInputStream();
                        inputStream.AddData(ptr, bytes.Length, null);

                        request.Finish(inputStream, bytes.Length, mimeType);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Hermes] Error serving {uri}: {ex.Message}");
                        FinishWithError(request);
                    }
                }
                else
                {
                    Console.WriteLine($"[Hermes] Handler returned null for {uri}");
                    FinishWithError(request);
                }
            }
            else
            {
                Console.WriteLine($"[Hermes] No handler found for scheme '{scheme}'");
                FinishWithError(request);
            }
        };

        // Store the callback to prevent GC
        _schemeCallbacks.Add(callback);

        context.RegisterUriScheme(scheme, callback);
        Console.WriteLine($"[Hermes] URI scheme '{scheme}' registered successfully");
        Console.Out.Flush();
    }

    private static void FinishWithError(WebKit.URISchemeRequest request)
    {
        // Return empty content with 404 status - FinishError requires GLib.Error which isn't available
        // as a public type in the current WebKitGTKSharp binding
        try
        {
            var emptyStream = new GLib.MemoryInputStream();
            request.Finish(emptyStream, 0, "text/plain");
        }
        catch
        {
            // If we can't even finish with empty content, just log and move on
            HermesLogger.Warning($"Failed to handle URI scheme request: {request.Uri}");
        }
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

    public void Invoke(System.Action action)
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

    public void BeginInvoke(System.Action action)
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
            catch (Exception ex)
            {
                HermesLogger.Error("Exception in BeginInvoke callback", ex);
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

    internal LinuxContextMenuBackend CreateContextMenuBackend()
    {
        ThrowIfNotInitialized();
        return new LinuxContextMenuBackend(_window);
    }

    internal Gtk.Window Window => _window;

    private static void EnsureGtkInitialized()
    {
        if (s_gtkInitialized) return;

        lock (s_initLock)
        {
            if (s_gtkInitialized) return;

            // Register native library resolver to handle webkit2gtk-4.0 -> 4.1 mapping
            // Ubuntu 24.04+ only ships webkit2gtk-4.1, but GtkSharp bindings expect 4.0
            RegisterWebKitLibraryResolver();

            Gtk.Application.Init();
            s_gtkInitialized = true;
        }
    }

    private static bool s_resolverRegistered;

    private static void RegisterWebKitLibraryResolver()
    {
        if (s_resolverRegistered) return;
        s_resolverRegistered = true;

        // GtkSharp uses its own GLibrary.Load mechanism with hardcoded library names.
        // The standard NativeLibrary.SetDllImportResolver doesn't work because GtkSharp
        // bypasses P/Invoke. We need to patch GLibrary's _libraryDefinitions dictionary
        // via reflection to add webkit2gtk-4.1 support for Ubuntu 24.04+.
        try
        {
            // Load WebkitGtkSharp assembly without triggering type initialization
            var webkitAssembly = Assembly.Load("WebkitGtkSharp");

            // Get the internal GLibrary class (compiled into each GtkSharp assembly)
            var gLibraryType = webkitAssembly.GetType("GLibrary", throwOnError: false);
            if (gLibraryType == null)
            {
                HermesLogger.Warning("Could not find GLibrary type for webkit2gtk-4.1 patching");
                return;
            }

            // Get the _libraryDefinitions dictionary field
            var definitionsField = gLibraryType.GetField("_libraryDefinitions",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (definitionsField == null)
            {
                HermesLogger.Warning("Could not find _libraryDefinitions field for webkit2gtk-4.1 patching");
                return;
            }

            // Get the Library enum type
            var libraryEnumType = webkitAssembly.GetType("Library", throwOnError: false);
            if (libraryEnumType == null)
            {
                HermesLogger.Warning("Could not find Library enum for webkit2gtk-4.1 patching");
                return;
            }

            // Get the Webkit enum value
            var webkitEnumValue = Enum.Parse(libraryEnumType, "Webkit");

            // Force the static constructor to run by accessing the field
            // This initializes _libraryDefinitions with the default (4.0) values
            var definitions = definitionsField.GetValue(null);
            if (definitions == null)
            {
                HermesLogger.Warning("_libraryDefinitions is null");
                return;
            }

            // The dictionary is Dictionary<Library, string[]> - use reflection to update it
            var dictionaryType = definitions.GetType();
            var indexer = dictionaryType.GetProperty("Item");

            // Set new library names that include webkit2gtk-4.1 variants first
            // On Ubuntu 24.04+, only 4.1 is available, so we try those first
            var newLibraryNames = new[]
            {
                "libwebkit2gtk-4.1.so.0",  // Ubuntu 24.04+ (try first)
                "libwebkit2gtk-4.1.so",
                "libwebkit2gtk-4.0.so.37", // Ubuntu 22.04 and older
                "libwebkit2gtk-4.0.dll",
                "libwebkit2gtk-4.0.dylib",
                "libwebkit2gtk-4.0.0.dll"
            };

            indexer?.SetValue(definitions, newLibraryNames, new[] { webkitEnumValue });
            HermesLogger.Info("Patched GLibrary to support webkit2gtk-4.1");
        }
        catch (Exception ex)
        {
            HermesLogger.Warning($"Failed to patch webkit2gtk library definitions: {ex.Message}");
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
            // Use JsValue property from the develop package
            var value = jsResult.JsValue;

            if (value.IsString)
            {
                var message = value.ToString();
                WebMessageReceived?.Invoke(message);
            }
        }
        catch (Exception ex)
        {
            HermesLogger.Warning($"Failed to parse JavaScript message result: {ex.Message}");
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Window not initialized. Call Initialize() first.");
    }
}
