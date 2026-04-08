// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Hermes.Abstractions;
using Microsoft.Web.WebView2.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Hermes.Platforms.Windows;

[SupportedOSPlatform("windows")]
internal sealed class WindowsWindowBackend : IHermesWindowBackend
{
    private const uint WM_USER_INVOKE = PInvoke.WM_USER + 0x0002;
    private const string WindowClassName = "HermesWindow";

    private static readonly ConcurrentDictionary<HWND, WindowsWindowBackend> s_hwndToInstance = new();
    private static HINSTANCE s_hInstance;
    private static bool s_classRegistered;
    private static readonly object s_registrationLock = new();
    private static readonly WNDPROC s_wndProc = WindowProc;

    private HWND _hwnd;
    private HermesWindowOptions _options = null!;
    private bool _isInitialized;
    private bool _isDisposed;
    private bool _firstShowComplete;
    private bool _webViewInitialized;
    private int _uiThreadId;

    private CoreWebView2Environment? _webViewEnvironment;
    private CoreWebView2Controller? _webViewController;
    private CoreWebView2? _webView;
    private TaskCompletionSource? _webViewReady;

    private readonly ConcurrentQueue<(Action Action, TaskCompletionSource Tcs)> _invokeQueue = new();
    private readonly Dictionary<string, Func<string, (Stream? Content, string? ContentType)>> _customSchemeHandlers = new();

    private WindowsMenuBackend? _menuBackend;
    private WindowsCustomTitlebar? _customTitlebar;

    public event Action? Closing;
    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action? FocusIn;
    public event Action? FocusOut;
    public event Action<string>? WebMessageReceived;
    public event Action? Maximized;
    public event Action? Restored;

    public void Initialize(HermesWindowOptions options)
    {
        if (_isInitialized)
            throw new InvalidOperationException("Window already initialized");

        // WebView2 requires an STA thread. Without [STAThread] on Main(), COM operations will fail.
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "Hermes requires the main thread to be an STA thread. " +
                "Add [STAThread] attribute to your Main method or use 'async Task Main(string[] args)' with [STAThread].");
        }

        _options = options;
        _uiThreadId = Environment.CurrentManagedThreadId;

        // CustomTitleBar on Windows uses DWM frame extension, NOT chromeless mode
        // This preserves native caption buttons while allowing custom titlebar content

        EnsureWindowClassRegistered();

        var style = CalculateWindowStyle(_options);
        var exStyle = CalculateExtendedStyle(_options);

        int x = options.X ?? PInvoke.CW_USEDEFAULT;
        int y = options.Y ?? PInvoke.CW_USEDEFAULT;

        if (options.CenterOnScreen)
        {
            x = PInvoke.CW_USEDEFAULT;
            y = PInvoke.CW_USEDEFAULT;
        }

        unsafe
        {
            fixed (char* className = WindowClassName)
            fixed (char* title = options.Title)
            {
                _hwnd = PInvoke.CreateWindowEx(
                    exStyle,
                    className,
                    title,
                    style,
                    x, y,
                    options.Width, options.Height,
                    HWND.Null,
                    HMENU.Null,
                    s_hInstance,
                    null);
            }
        }

        if (_hwnd.IsNull)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create window");

        s_hwndToInstance[_hwnd] = this;

        // Initialize custom titlebar if enabled (after window created, before showing)
        if (options.CustomTitleBar)
        {
            _customTitlebar = new WindowsCustomTitlebar(_hwnd);
            _customTitlebar.Initialize();
        }

        if (!string.IsNullOrEmpty(options.IconPath))
            SetIcon(options.IconPath);

        if (options.CenterOnScreen)
            CenterWindow();

        _isInitialized = true;
    }

    public void Show()
    {
        ThrowIfNotInitialized();

        SHOW_WINDOW_CMD showCmd;
        if (!_firstShowComplete)
        {
            showCmd = _options.Maximized ? SHOW_WINDOW_CMD.SW_MAXIMIZE
                : _options.Minimized ? SHOW_WINDOW_CMD.SW_MINIMIZE
                : SHOW_WINDOW_CMD.SW_SHOW;
            _firstShowComplete = true;
        }
        else
        {
            showCmd = SHOW_WINDOW_CMD.SW_SHOW;
        }

        PInvoke.ShowWindow(_hwnd, showCmd);
        PInvoke.UpdateWindow(_hwnd);

        if (!_webViewInitialized)
        {
            _webViewInitialized = true;
            _ = InitializeWebViewAsync();
        }
    }

    public void Hide()
    {
        ThrowIfNotInitialized();
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    public void WaitForClose()
    {
        var isSmokeTest = Environment.GetEnvironmentVariable("HERMES_SMOKE_TEST") == "1";

        Show();

        if (isSmokeTest) Console.WriteLine($"WAITFORCLOSE:entered,webViewReady={_webViewReady is not null}");

        // Pump messages until WebView is initialized
        // This allows async continuations from InitializeWebViewAsync to run
        if (_webViewReady is not null)
        {
            if (isSmokeTest) Console.WriteLine("WAITFORCLOSE:pumping_messages");
            while (!_webViewReady.Task.IsCompleted)
            {
                if (PInvoke.PeekMessage(out var msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                {
                    PInvoke.TranslateMessage(in msg);
                    PInvoke.DispatchMessage(in msg);
                }
                else
                {
                    Thread.Sleep(1); // Avoid busy-waiting
                }
            }
            if (isSmokeTest) Console.WriteLine("WAITFORCLOSE:webview_ready");
        }

        if (isSmokeTest) Console.WriteLine("WAITFORCLOSE:entering_main_loop");
        RunMessageLoop();
    }

    public void Close()
    {
        if (_isInitialized && !_hwnd.IsNull)
        {
            PInvoke.PostMessage(_hwnd, PInvoke.WM_CLOSE, 0, 0);
        }
    }

    public string Title
    {
        get
        {
            ThrowIfNotInitialized();
            int length = PInvoke.GetWindowTextLength(_hwnd) + 1;
            if (length <= 1) return string.Empty;

            unsafe
            {
                Span<char> buffer = stackalloc char[length];
                fixed (char* ptr = buffer)
                {
                    PInvoke.GetWindowText(_hwnd, ptr, length);
                }
                return new string(buffer.TrimEnd('\0'));
            }
        }
        set
        {
            ThrowIfNotInitialized();
            PInvoke.SetWindowText(_hwnd, value);
        }
    }

    public (int Width, int Height) Size
    {
        get
        {
            ThrowIfNotInitialized();
            PInvoke.GetWindowRect(_hwnd, out var rect);
            return (rect.right - rect.left, rect.bottom - rect.top);
        }
        set
        {
            ThrowIfNotInitialized();
            PInvoke.SetWindowPos(_hwnd, HWND.Null, 0, 0, value.Width, value.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        }
    }

    public (int X, int Y) Position
    {
        get
        {
            ThrowIfNotInitialized();
            PInvoke.GetWindowRect(_hwnd, out var rect);
            return (rect.left, rect.top);
        }
        set
        {
            ThrowIfNotInitialized();
            PInvoke.SetWindowPos(_hwnd, HWND.Null, value.X, value.Y, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
        }
    }

    public bool IsMaximized
    {
        get
        {
            ThrowIfNotInitialized();
            return PInvoke.IsZoomed(_hwnd);
        }
        set
        {
            ThrowIfNotInitialized();
            var wasMaximized = PInvoke.IsZoomed(_hwnd);
            PInvoke.ShowWindow(_hwnd, value ? SHOW_WINDOW_CMD.SW_MAXIMIZE : SHOW_WINDOW_CMD.SW_RESTORE);

            // Fire events if state changed
            if (value && !wasMaximized)
                Maximized?.Invoke();
            else if (!value && wasMaximized)
                Restored?.Invoke();
        }
    }

    public bool IsMinimized
    {
        get
        {
            ThrowIfNotInitialized();
            return PInvoke.IsIconic(_hwnd);
        }
        set
        {
            ThrowIfNotInitialized();
            PInvoke.ShowWindow(_hwnd, value ? SHOW_WINDOW_CMD.SW_MINIMIZE : SHOW_WINDOW_CMD.SW_RESTORE);
        }
    }

    public HermesPlatform Platform => HermesPlatform.Windows;

    public bool IsCustomTitleBarActive => _options.CustomTitleBar;

    public void NavigateToUrl(string url)
    {
        ThrowIfNotInitialized();
        if (_webView is not null)
        {
            _webView.Navigate(url);
        }
        else
        {
            _options.StartUrl = url;
            _options.StartHtml = null;
        }
    }

    public void NavigateToString(string html)
    {
        ThrowIfNotInitialized();
        if (_webView is not null)
        {
            _webView.NavigateToString(html);
        }
        else
        {
            _options.StartHtml = html;
            _options.StartUrl = null;
        }
    }

    public void SendWebMessage(string message)
    {
        ThrowIfNotInitialized();
        // Use JSON serialization for consistent escaping across platforms
        var json = JsonSerializer.Serialize(message);
        _webView?.PostWebMessageAsJson(json);
    }

    public void RegisterCustomScheme(string scheme, Func<string, (Stream? Content, string? ContentType)> handler)
    {
        _customSchemeHandlers[scheme] = handler;

        if (_webView is not null)
        {
            _webView.AddWebResourceRequestedFilter($"{scheme}://*", CoreWebView2WebResourceContext.All);
        }
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
        _invokeQueue.Enqueue((action, tcs));

        PInvoke.PostMessage(_hwnd, WM_USER_INVOKE, 0, 0);

        tcs.Task.GetAwaiter().GetResult();
    }

    public void BeginInvoke(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        // Fire-and-forget: enqueue without waiting
        _invokeQueue.Enqueue((action, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)));
        PInvoke.PostMessage(_hwnd, WM_USER_INVOKE, 0, 0);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _webViewController?.Close();
        _webViewController = null;
        _webView = null;
        _webViewEnvironment = null;

        if (!_hwnd.IsNull)
        {
            s_hwndToInstance.TryRemove(_hwnd, out _);
            if (_isInitialized)
            {
                PInvoke.DestroyWindow(_hwnd);
            }
            _hwnd = HWND.Null;
        }
    }

    internal WindowsMenuBackend CreateMenuBackend()
    {
        ThrowIfNotInitialized();
        // When custom titlebar is enabled, don't use native menu bar
        // Menus should be rendered in the WebView instead
        bool useNativeMenu = _customTitlebar is null;
        _menuBackend ??= new WindowsMenuBackend(_hwnd, useNativeMenu);
        return _menuBackend;
    }

    internal WindowsDialogBackend CreateDialogBackend()
    {
        ThrowIfNotInitialized();
        return new WindowsDialogBackend(_hwnd);
    }

    internal WindowsContextMenuBackend CreateContextMenuBackend()
    {
        ThrowIfNotInitialized();
        return new WindowsContextMenuBackend(_hwnd);
    }

    internal HWND Handle => _hwnd;

    private static void EnsureWindowClassRegistered()
    {
        if (s_classRegistered) return;

        lock (s_registrationLock)
        {
            if (s_classRegistered) return;

            s_hInstance = PInvoke.GetModuleHandle((PCWSTR)null);

            // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 is defined as ((DPI_AWARENESS_CONTEXT)-4)
            PInvoke.SetThreadDpiAwarenessContext(new DPI_AWARENESS_CONTEXT((nint)(-4)));

            unsafe
            {
                fixed (char* className = WindowClassName)
                {
                    var wcx = new WNDCLASSEXW
                    {
                        cbSize = (uint)sizeof(WNDCLASSEXW),
                        style = WNDCLASS_STYLES.CS_HREDRAW | WNDCLASS_STYLES.CS_VREDRAW,
                        lpfnWndProc = s_wndProc,
                        hInstance = s_hInstance,
                        hCursor = PInvoke.LoadCursor(HINSTANCE.Null, PInvoke.IDC_ARROW),
                        hbrBackground = new HBRUSH((nint)((int)SYS_COLOR_INDEX.COLOR_WINDOW + 1)),
                        lpszClassName = className
                    };

                    var atom = PInvoke.RegisterClassEx(in wcx);
                    if (atom == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register window class");
                }
            }

            s_classRegistered = true;
        }
    }

    private static LRESULT WindowProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        if (!s_hwndToInstance.TryGetValue(hwnd, out var instance))
            return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);

        // Handle custom titlebar messages first if enabled
        if (instance._customTitlebar is not null)
        {
            switch (uMsg)
            {
                case PInvoke.WM_NCCALCSIZE:
                    return instance._customTitlebar.HandleNcCalcSize(wParam, lParam);

                case PInvoke.WM_NCHITTEST:
                    var result = instance._customTitlebar.HandleNcHitTest(lParam, out var handled);
                    if (handled)
                        return result;
                    break;

                case PInvoke.WM_NCACTIVATE:
                    // Always return TRUE to prevent visual changes, let DWM handle rendering
                    return new LRESULT(1);

                case PInvoke.WM_DPICHANGED:
                    instance.HandleDpiChanged(wParam, lParam);
                    break;
            }
        }

        return uMsg switch
        {
            PInvoke.WM_CLOSE => instance.HandleClose(),
            PInvoke.WM_DESTROY => instance.HandleDestroy(),
            PInvoke.WM_SIZE => instance.HandleSize(lParam),
            PInvoke.WM_MOVE => instance.HandleMove(lParam),
            PInvoke.WM_ACTIVATE => instance.HandleActivate(wParam),
            PInvoke.WM_GETMINMAXINFO => instance.HandleGetMinMaxInfo(lParam),
            PInvoke.WM_COMMAND => instance.HandleCommand(wParam),
            WM_USER_INVOKE => instance.HandleUserInvoke(),
            _ => PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam)
        };
    }

    private LRESULT HandleClose()
    {
        Closing?.Invoke();
        PInvoke.DestroyWindow(_hwnd);
        return new LRESULT(0);
    }

    private LRESULT HandleDestroy()
    {
        s_hwndToInstance.TryRemove(_hwnd, out _);

        _webViewController?.Close();
        _webViewController = null;
        _webView = null;

        PInvoke.PostQuitMessage(0);
        return new LRESULT(0);
    }

    private LRESULT HandleSize(LPARAM lParam)
    {
        RefitContent();

        int width = (int)(lParam.Value & 0xFFFF);
        int height = (int)((lParam.Value >> 16) & 0xFFFF);
        Resized?.Invoke(width, height);

        return new LRESULT(0);
    }

    private LRESULT HandleMove(LPARAM lParam)
    {
        int x = (int)(short)(lParam.Value & 0xFFFF);
        int y = (int)(short)((lParam.Value >> 16) & 0xFFFF);
        Moved?.Invoke(x, y);

        return new LRESULT(0);
    }

    private LRESULT HandleActivate(WPARAM wParam)
    {
        var activateState = (int)(wParam.Value & 0xFFFF);

        if (activateState == PInvoke.WA_INACTIVE)
            FocusOut?.Invoke();
        else
            FocusIn?.Invoke();

        return PInvoke.DefWindowProc(_hwnd, PInvoke.WM_ACTIVATE, wParam, 0);
    }

    private LRESULT HandleGetMinMaxInfo(LPARAM lParam)
    {
        unsafe
        {
            var mmi = (MINMAXINFO*)lParam.Value;

            if (_options.MinWidth.HasValue)
                mmi->ptMinTrackSize.X = _options.MinWidth.Value;
            if (_options.MinHeight.HasValue)
                mmi->ptMinTrackSize.Y = _options.MinHeight.Value;
            if (_options.MaxWidth.HasValue)
                mmi->ptMaxTrackSize.X = _options.MaxWidth.Value;
            if (_options.MaxHeight.HasValue)
                mmi->ptMaxTrackSize.Y = _options.MaxHeight.Value;
        }

        return new LRESULT(0);
    }

    private LRESULT HandleCommand(WPARAM wParam)
    {
        uint menuId = (uint)(wParam.Value & 0xFFFF);
        _menuBackend?.HandleMenuCommand(menuId);

        return new LRESULT(0);
    }

    private LRESULT HandleUserInvoke()
    {
        while (_invokeQueue.TryDequeue(out var item))
        {
            try
            {
                item.Action();
                item.Tcs.SetResult();
            }
            catch (Exception ex)
            {
                item.Tcs.SetException(ex);
            }
        }
        return new LRESULT(0);
    }

    private void HandleDpiChanged(WPARAM wParam, LPARAM lParam)
    {
        int newDpi = (int)(wParam.Value & 0xFFFF);
        _customTitlebar?.UpdateDpi(newDpi);

        // lParam contains a pointer to the suggested new window rect
        unsafe
        {
            var pRect = (RECT*)lParam.Value;
            PInvoke.SetWindowPos(_hwnd, HWND.Null,
                pRect->left, pRect->top,
                pRect->right - pRect->left,
                pRect->bottom - pRect->top,
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        _webViewReady = new TaskCompletionSource();
        var isSmokeTest = Environment.GetEnvironmentVariable("HERMES_SMOKE_TEST") == "1";

        try
        {
            if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:starting");

            // Use pre-warmed environment from pool (instant if Prewarm() was called)
            // Note: No ConfigureAwait(false) - we need continuations on UI thread for WebView2
            _webViewEnvironment = await WebView2EnvironmentPool.Instance
                .GetOrCreateEnvironmentAsync();

            if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:environment_ready");

            _webViewController = await _webViewEnvironment.CreateCoreWebView2ControllerAsync(_hwnd);
            _webView = _webViewController.CoreWebView2;

            if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:controller_ready");

            var settings = _webView.Settings;
            settings.AreDefaultContextMenusEnabled = _options.ContextMenuEnabled;
            settings.AreDevToolsEnabled = _options.DevToolsEnabled;
            settings.IsScriptEnabled = true;
            settings.IsWebMessageEnabled = true;

            await _webView.AddScriptToExecuteOnDocumentCreatedAsync(
                """
                window.external = {
                    sendMessage: function(message) { window.chrome.webview.postMessage(message); },
                    receiveMessage: function(callback) {
                        window.chrome.webview.addEventListener('message', function(e) { callback(e.data); });
                    }
                };
                """);

            if (_options.CustomTitleBar)
            {
                await _webView.AddScriptToExecuteOnDocumentCreatedAsync(GetDragDetectionScript());
            }

            if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:script_injected");

            _webView.WebMessageReceived += (sender, args) =>
            {
                var message = args.TryGetWebMessageAsString();

                if (_customTitlebar is not null && message?.StartsWith("{\"type\":\"hermes-drag\"") == true)
                {
                    HandleDragMessage(message);
                    return;
                }

                if (message is not null)
                    WebMessageReceived?.Invoke(message);
            };

            _webView.WebResourceRequested += HandleWebResourceRequested;
            _webView.ProcessFailed += HandleWebViewProcessFailed;
            foreach (var scheme in _customSchemeHandlers.Keys)
            {
                var filter = $"{scheme}://*";
                if (isSmokeTest) Console.WriteLine($"WEBVIEW_INIT:adding_filter:{filter}");
                _webView.AddWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.All);
            }

            RefitContent();

            if (!string.IsNullOrEmpty(_options.StartUrl))
            {
                if (isSmokeTest) Console.WriteLine($"WEBVIEW_INIT:navigating_to:{_options.StartUrl}");
                _webView.Navigate(_options.StartUrl);
            }
            else if (!string.IsNullOrEmpty(_options.StartHtml))
            {
                if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:navigating_to_html");
                _webView.NavigateToString(_options.StartHtml);
            }
            else
            {
                if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:no_navigation_url");
            }

            if (isSmokeTest) Console.WriteLine("WEBVIEW_INIT:complete");
            _webViewReady.SetResult();
        }
        catch (Exception ex)
        {
            if (isSmokeTest) Console.WriteLine($"WEBVIEW_INIT:error:{ex.Message}");
            _webViewReady.SetException(ex);
        }
    }

    private void HandleWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var isSmokeTest = Environment.GetEnvironmentVariable("HERMES_SMOKE_TEST") == "1";
        if (isSmokeTest) Console.WriteLine($"RESOURCE_REQUEST:{e.Request.Uri}");

        try
        {
            var uri = new Uri(e.Request.Uri);
            var scheme = uri.Scheme;

            if (_customSchemeHandlers.TryGetValue(scheme, out var handler))
            {
                var (stream, handlerContentType) = handler(e.Request.Uri);
                if (stream is not null)
                {
                    // Use Content-Type from handler if provided, otherwise fall back to extension-based detection
                    var contentType = handlerContentType ?? GetContentType(uri.AbsolutePath);
                    if (isSmokeTest) Console.WriteLine($"RESOURCE_RESPONSE:200,{contentType},{stream.Length}bytes");
                    var headers = $"Content-Type: {contentType}\r\n" +
                                  "Access-Control-Allow-Origin: *\r\n" +
                                  "Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS\r\n" +
                                  "Access-Control-Allow-Headers: *";
                    var response = _webViewEnvironment!.CreateWebResourceResponse(
                        stream,
                        200,
                        "OK",
                        headers);
                    e.Response = response;
                }
                else
                {
                    if (isSmokeTest) Console.WriteLine($"RESOURCE_RESPONSE:404,handler_returned_null");
                    var headers = "Access-Control-Allow-Origin: *";
                    var response = _webViewEnvironment!.CreateWebResourceResponse(
                        null,
                        404,
                        "Not Found",
                        headers);
                    e.Response = response;
                }
            }
            else
            {
                if (isSmokeTest) Console.WriteLine($"RESOURCE_RESPONSE:no_handler_for_scheme:{scheme}");
            }
        }
        catch (Exception ex)
        {
            if (isSmokeTest) Console.WriteLine($"RESOURCE_ERROR:{ex.GetType().Name}:{ex.Message}");
        }
    }

    private void HandleWebViewProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        if (!Diagnostics.HermesCrashInterceptor.IsEnabled)
            return;

        var reason = e.ProcessFailedKind.ToString();
        var description = e.Reason.ToString();
        var message = $"WebView2 process failed: {reason} ({description})";

        var context = Diagnostics.HermesCrashInterceptor.BuildCrashContext(
            new InvalidOperationException(message), Contracts.Diagnostics.CrashSource.WebViewCrash);

        Diagnostics.HermesCrashInterceptor.NotifyCrash(context);
    }

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        // Paths without extensions (like "/" or "/page") are HTML pages (Blazor routing)
        if (string.IsNullOrEmpty(extension))
            return "text/html";

        return extension switch
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
            ".wasm" => "application/wasm",
            ".dll" => "application/octet-stream",
            ".pdb" => "application/octet-stream",
            ".dat" => "application/octet-stream",
            ".blat" => "application/octet-stream",
            _ => "application/octet-stream"
        };
    }

    private void RefitContent()
    {
        if (_webViewController is null || _hwnd.IsNull) return;

        PInvoke.GetClientRect(_hwnd, out var rect);

        // WebView fills entire client area
        // App is responsible for rendering titlebar in HTML when CustomTitleBar is enabled
        _webViewController.Bounds = new System.Drawing.Rectangle(
            0, 0, rect.right - rect.left, rect.bottom - rect.top);
    }

    private void RunMessageLoop()
    {
        MSG msg;
        while (PInvoke.GetMessage(out msg, HWND.Null, 0, 0))
        {
            // Check for keyboard accelerators (menu shortcuts)
            if (_menuBackend is not null)
            {
                var hAccel = _menuBackend.AccelTable;
                if (!hAccel.IsNull && PInvoke.TranslateAccelerator(_hwnd, hAccel, in msg) != 0)
                    continue; // Accelerator was handled, skip normal processing
            }

            PInvoke.TranslateMessage(in msg);
            PInvoke.DispatchMessage(in msg);
        }
    }

    private static WINDOW_STYLE CalculateWindowStyle(HermesWindowOptions options)
    {
        if (options.Chromeless)
            return WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE;

        var style = WINDOW_STYLE.WS_OVERLAPPEDWINDOW;

        if (!options.Resizable)
        {
            style &= ~(WINDOW_STYLE.WS_THICKFRAME |
                       WINDOW_STYLE.WS_MINIMIZEBOX |
                       WINDOW_STYLE.WS_MAXIMIZEBOX);
        }

        return style;
    }

    private static WINDOW_EX_STYLE CalculateExtendedStyle(HermesWindowOptions options)
    {
        var exStyle = HermesApplication.IsAccessoryMode
            ? WINDOW_EX_STYLE.WS_EX_TOOLWINDOW
            : WINDOW_EX_STYLE.WS_EX_APPWINDOW;

        if (options.TopMost)
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOPMOST;

        return exStyle;
    }

    private void SetIcon(string iconPath)
    {
        if (!File.Exists(iconPath)) return;

        unsafe
        {
            fixed (char* path = iconPath)
            {
                var hIcon = PInvoke.LoadImage(
                    HINSTANCE.Null,
                    path,
                    GDI_IMAGE_TYPE.IMAGE_ICON,
                    0, 0,
                    IMAGE_FLAGS.LR_LOADFROMFILE | IMAGE_FLAGS.LR_DEFAULTSIZE);

                if (!hIcon.IsNull)
                {
                    PInvoke.SendMessage(_hwnd, PInvoke.WM_SETICON, 1, new LPARAM((nint)hIcon.Value));
                    PInvoke.SendMessage(_hwnd, PInvoke.WM_SETICON, 0, new LPARAM((nint)hIcon.Value));
                }
            }
        }
    }

    private void CenterWindow()
    {
        PInvoke.GetWindowRect(_hwnd, out var rect);
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;

        int screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        int screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

        int x = (screenWidth - width) / 2;
        int y = (screenHeight - height) / 2;

        PInvoke.SetWindowPos(_hwnd, HWND.Null, x, y, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);
    }

    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Window not initialized. Call Initialize() first.");
    }

    #region Custom Titlebar Drag Support

    /// <summary>
    /// Returns JavaScript that detects -webkit-app-region: drag/no-drag CSS properties
    /// and posts messages to native code for window dragging. Matches the macOS implementation.
    /// </summary>
    private static string GetDragDetectionScript() => """
        (function() {
            // Check if element or any ancestor has app-region: drag (and isn't blocked by no-drag)
            function __hermesIsDragRegion(el) {
                while (el) {
                    var style = window.getComputedStyle(el);
                    var region = style.getPropertyValue('-webkit-app-region') ||
                                 style.getPropertyValue('app-region');
                    if (region === 'no-drag') return false;
                    if (region === 'drag') return true;
                    // Also check for hermes-specific classes/attributes
                    if (el.classList && el.classList.contains('hermes-no-drag')) return false;
                    if (el.hasAttribute && el.hasAttribute('data-hermes-drag')) return true;
                    el = el.parentElement;
                }
                return false;
            }

            // Track click timing for double-click detection
            var lastClickTime = 0;
            var doubleClickThreshold = 500;

            document.addEventListener('mousedown', function(e) {
                // Only handle left mouse button
                if (e.button !== 0) return;

                if (!__hermesIsDragRegion(e.target)) {
                    window.external.sendMessage(JSON.stringify({type:'hermes-drag', action:'no-drag'}));
                    return;
                }

                var now = Date.now();
                if (now - lastClickTime < doubleClickThreshold) {
                    // Double-click on drag region - toggle maximize
                    window.external.sendMessage(JSON.stringify({type:'hermes-drag', action:'double-click'}));
                    lastClickTime = 0;
                } else {
                    // Single click - initiate drag
                    window.external.sendMessage(JSON.stringify({type:'hermes-drag', action:'drag'}));
                    lastClickTime = now;
                }
            });
        })();
        """;

    /// <summary>
    /// Handles drag-related messages from JavaScript for custom titlebar window dragging.
    /// </summary>
    private void HandleDragMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var action = doc.RootElement.GetProperty("action").GetString();

            switch (action)
            {
                case "drag":
                    // Initiate window drag using Windows system drag
                    // ReleaseCapture allows the window to receive the drag
                    // WM_NCLBUTTONDOWN with HTCAPTION tells Windows to start a title bar drag
                    PInvoke.ReleaseCapture();
                    PInvoke.SendMessage(_hwnd, PInvoke.WM_NCLBUTTONDOWN, (WPARAM)PInvoke.HTCAPTION, 0);
                    break;

                case "double-click":
                    // Toggle maximize on double-click (standard Windows behavior)
                    if (PInvoke.IsZoomed(_hwnd))
                    {
                        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
                        Restored?.Invoke();
                    }
                    else
                    {
                        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_MAXIMIZE);
                        Maximized?.Invoke();
                    }
                    break;

                case "no-drag":
                    // Click was on a non-draggable element, no action needed
                    break;
            }
        }
        catch
        {
            // Ignore malformed messages
        }
    }

    #endregion
}
