using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Hermes.Abstractions;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of IHermesWindowBackend using native Objective-C library.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacWindowBackend : IHermesWindowBackend
{
    private IntPtr _windowHandle;
    private int _uiThreadId;
    private bool _initialized;
    private bool _disposed;

    // Keep strong references to delegates to prevent GC during window lifetime
    private readonly List<Delegate> _pinnedDelegates = new();

    // Custom scheme handlers registered before Initialize()
    private readonly Dictionary<string, Func<string, Stream?>> _customSchemeHandlers = new();
    private MacNativeDelegates.CustomSchemeCallback? _customSchemeCallback;

    // Cached options for deferred initialization
    private string _pendingTitle = "Hermes Window";
    private int _pendingWidth = 800;
    private int _pendingHeight = 600;

    #region Lifecycle

    public void Initialize(HermesWindowOptions options)
    {
        if (_initialized)
            throw new InvalidOperationException("Window has already been initialized.");

        _uiThreadId = Environment.CurrentManagedThreadId;

        // Create native parameter struct
        var parameters = new HermesWindowParams
        {
            Width = options.Width,
            Height = options.Height,
            MinWidth = options.MinWidth ?? 0,
            MinHeight = options.MinHeight ?? 0,
            MaxWidth = options.MaxWidth ?? 0,
            MaxHeight = options.MaxHeight ?? 0,
            UsePosition = options.X.HasValue && options.Y.HasValue,
            X = options.X ?? 0,
            Y = options.Y ?? 0,
            CenterOnScreen = options.CenterOnScreen,
            Chromeless = options.Chromeless,
            Resizable = options.Resizable,
            TopMost = options.TopMost,
            Maximized = options.Maximized,
            Minimized = options.Minimized,
            DevToolsEnabled = options.DevToolsEnabled,
            ContextMenuEnabled = options.ContextMenuEnabled
        };

        // Marshal strings
        parameters.Title = MarshalString(options.Title);
        parameters.StartUrl = MarshalString(options.StartUrl);
        parameters.StartHtml = MarshalString(options.StartHtml);
        parameters.IconPath = MarshalString(options.IconPath);

        // Initialize custom scheme names array (must be done before WindowCreate on macOS)
        parameters.CustomSchemeNames = new IntPtr[16];
        var schemeNames = _customSchemeHandlers.Keys.ToArray();
        for (int i = 0; i < Math.Min(schemeNames.Length, 16); i++)
        {
            parameters.CustomSchemeNames[i] = MarshalString(schemeNames[i]);
        }

        try
        {
            // Set up callbacks
            SetupCallbacks(ref parameters);

            // Create the window (schemes are registered from CustomSchemeNames during creation)
            _windowHandle = MacNative.WindowCreate(ref parameters);
            if (_windowHandle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create native window.");

            _initialized = true;
        }
        finally
        {
            // Free marshaled strings
            FreeString(parameters.Title);
            FreeString(parameters.StartUrl);
            FreeString(parameters.StartHtml);
            FreeString(parameters.IconPath);

            // Free scheme name strings
            foreach (var ptr in parameters.CustomSchemeNames)
            {
                FreeString(ptr);
            }
        }
    }

    public void Show()
    {
        EnsureInitialized();
        MacNative.WindowShow(_windowHandle);
    }

    public void Close()
    {
        if (_initialized && _windowHandle != IntPtr.Zero)
        {
            MacNative.WindowClose(_windowHandle);
        }
    }

    public void WaitForClose()
    {
        EnsureInitialized();
        MacNative.WindowWaitForClose(_windowHandle);
    }

    #endregion

    #region Window Properties

    public string Title
    {
        get
        {
            if (!_initialized) return _pendingTitle;

            var buffer = Marshal.AllocHGlobal(1024);
            try
            {
                MacNative.WindowGetTitle(_windowHandle, buffer, 1024);
                return Marshal.PtrToStringUTF8(buffer) ?? "";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        set
        {
            if (!_initialized)
            {
                _pendingTitle = value;
                return;
            }
            MacNative.WindowSetTitle(_windowHandle, value);
        }
    }

    public (int Width, int Height) Size
    {
        get
        {
            if (!_initialized) return (_pendingWidth, _pendingHeight);

            MacNative.WindowGetSize(_windowHandle, out int width, out int height);
            return (width, height);
        }
        set
        {
            if (!_initialized)
            {
                _pendingWidth = value.Width;
                _pendingHeight = value.Height;
                return;
            }
            MacNative.WindowSetSize(_windowHandle, value.Width, value.Height);
        }
    }

    public (int X, int Y) Position
    {
        get
        {
            EnsureInitialized();
            MacNative.WindowGetPosition(_windowHandle, out int x, out int y);
            return (x, y);
        }
        set
        {
            EnsureInitialized();
            MacNative.WindowSetPosition(_windowHandle, value.X, value.Y);
        }
    }

    public bool IsMaximized
    {
        get
        {
            if (!_initialized) return false;
            return MacNative.WindowGetIsMaximized(_windowHandle);
        }
        set
        {
            EnsureInitialized();
            MacNative.WindowSetIsMaximized(_windowHandle, value);
        }
    }

    public bool IsMinimized
    {
        get
        {
            if (!_initialized) return false;
            return MacNative.WindowGetIsMinimized(_windowHandle);
        }
        set
        {
            EnsureInitialized();
            MacNative.WindowSetIsMinimized(_windowHandle, value);
        }
    }

    #endregion

    #region WebView

    public void NavigateToUrl(string url)
    {
        EnsureInitialized();
        MacNative.WindowNavigateToUrl(_windowHandle, url);
    }

    public void NavigateToString(string html)
    {
        EnsureInitialized();
        MacNative.WindowNavigateToString(_windowHandle, html);
    }

    public void SendWebMessage(string message)
    {
        EnsureInitialized();
        MacNative.WindowSendWebMessage(_windowHandle, message);
    }

    public void RegisterCustomScheme(string scheme, Func<string, Stream?> handler)
    {
        if (_initialized)
        {
            // After initialization, only allow updating handlers for pre-registered schemes
            if (!_customSchemeHandlers.ContainsKey(scheme))
            {
                throw new InvalidOperationException(
                    "Custom schemes must be registered before Initialize() is called on macOS. " +
                    "Register schemes before calling Show() or WaitForClose().");
            }
            // Update the handler for an existing scheme
            _customSchemeHandlers[scheme] = handler;
            return;
        }

        _customSchemeHandlers[scheme] = handler;
    }

    #endregion

    #region Threading

    public int UIThreadId => _uiThreadId;

    public bool CheckAccess()
    {
        return Environment.CurrentManagedThreadId == _uiThreadId;
    }

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        Exception? capturedException = null;
        using var waitHandle = new ManualResetEventSlim(false);

        var invokeDelegate = new MacNativeDelegates.InvokeCallback(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
            finally
            {
                waitHandle.Set();
            }
        });

        // Pin delegate during native call
        var handle = GCHandle.Alloc(invokeDelegate);
        try
        {
            MacNative.WindowInvoke(_windowHandle, Marshal.GetFunctionPointerForDelegate(invokeDelegate));
            waitHandle.Wait();
        }
        finally
        {
            handle.Free();
        }

        if (capturedException is not null)
        {
            throw capturedException;
        }
    }

    public void BeginInvoke(Action action)
    {
        var invokeDelegate = new MacNativeDelegates.InvokeCallback(() =>
        {
            try
            {
                action();
            }
            catch
            {
                // Swallow exceptions in async invoke
            }
        });

        // Keep delegate alive - add to pinned list
        _pinnedDelegates.Add(invokeDelegate);

        MacNative.WindowBeginInvoke(_windowHandle, Marshal.GetFunctionPointerForDelegate(invokeDelegate));
    }

    #endregion

    #region Events

    public event Action? Closing;
    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action? FocusIn;
    public event Action? FocusOut;
    public event Action<string>? WebMessageReceived;

    #endregion

    #region Factory Methods

    internal MacMenuBackend CreateMenuBackend()
    {
        EnsureInitialized();
        return new MacMenuBackend(_windowHandle);
    }

    internal MacDialogBackend CreateDialogBackend()
    {
        return new MacDialogBackend();
    }

    internal MacContextMenuBackend CreateContextMenuBackend()
    {
        EnsureInitialized();
        return new MacContextMenuBackend(_windowHandle);
    }

    #endregion

    #region Private Helpers

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Window has not been initialized. Call Initialize() first.");
    }

    private void SetupCallbacks(ref HermesWindowParams parameters)
    {
        // Create delegates
        var closingDelegate = new MacNativeDelegates.ClosingCallback(OnNativeClosing);
        var resizedDelegate = new MacNativeDelegates.ResizedCallback(OnNativeResized);
        var movedDelegate = new MacNativeDelegates.MovedCallback(OnNativeMoved);
        var focusInDelegate = new MacNativeDelegates.FocusCallback(OnNativeFocusIn);
        var focusOutDelegate = new MacNativeDelegates.FocusCallback(OnNativeFocusOut);
        var webMessageDelegate = new MacNativeDelegates.WebMessageCallback(OnNativeWebMessage);

        // Store strong references to prevent GC
        _pinnedDelegates.Add(closingDelegate);
        _pinnedDelegates.Add(resizedDelegate);
        _pinnedDelegates.Add(movedDelegate);
        _pinnedDelegates.Add(focusInDelegate);
        _pinnedDelegates.Add(focusOutDelegate);
        _pinnedDelegates.Add(webMessageDelegate);

        // Set up custom scheme callback if we have any handlers
        if (_customSchemeHandlers.Count > 0)
        {
            _customSchemeCallback = new MacNativeDelegates.CustomSchemeCallback(OnNativeCustomScheme);
            _pinnedDelegates.Add(_customSchemeCallback);
            parameters.OnCustomScheme = Marshal.GetFunctionPointerForDelegate(_customSchemeCallback);
        }

        // Convert to function pointers
        parameters.OnClosing = Marshal.GetFunctionPointerForDelegate(closingDelegate);
        parameters.OnResized = Marshal.GetFunctionPointerForDelegate(resizedDelegate);
        parameters.OnMoved = Marshal.GetFunctionPointerForDelegate(movedDelegate);
        parameters.OnFocusIn = Marshal.GetFunctionPointerForDelegate(focusInDelegate);
        parameters.OnFocusOut = Marshal.GetFunctionPointerForDelegate(focusOutDelegate);
        parameters.OnWebMessage = Marshal.GetFunctionPointerForDelegate(webMessageDelegate);
    }

    private void OnNativeClosing()
    {
        Closing?.Invoke();
    }

    private void OnNativeResized(int width, int height)
    {
        Resized?.Invoke(width, height);
    }

    private void OnNativeMoved(int x, int y)
    {
        Moved?.Invoke(x, y);
    }

    private void OnNativeFocusIn()
    {
        FocusIn?.Invoke();
    }

    private void OnNativeFocusOut()
    {
        FocusOut?.Invoke();
    }

    private void OnNativeWebMessage(IntPtr messagePtr)
    {
        var message = Marshal.PtrToStringUTF8(messagePtr) ?? "";
        WebMessageReceived?.Invoke(message);
    }

    private IntPtr OnNativeCustomScheme(IntPtr urlPtr, out int numBytes, out IntPtr contentTypePtr)
    {
        numBytes = 0;
        contentTypePtr = IntPtr.Zero;

        var url = Marshal.PtrToStringUTF8(urlPtr) ?? "";

        try
        {
            var uri = new Uri(url);
            var scheme = uri.Scheme;

            if (_customSchemeHandlers.TryGetValue(scheme, out var handler))
            {
                var stream = handler(url);
                if (stream is not null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    var data = ms.ToArray();

                    numBytes = data.Length;
                    var resultPtr = Marshal.AllocHGlobal(numBytes);
                    Marshal.Copy(data, 0, resultPtr, numBytes);

                    // Allocate content type string (native code will free)
                    var contentType = GetContentType(uri.AbsolutePath);
                    contentTypePtr = Marshal.StringToHGlobalAnsi(contentType);

                    return resultPtr;
                }
            }
        }
        catch
        {
            // Return null for errors
        }

        return IntPtr.Zero;
    }

    private static string GetContentType(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        // Root URL (/) serves index.html
        if (string.IsNullOrEmpty(extension) && (path == "/" || string.IsNullOrEmpty(path)))
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

    private static IntPtr MarshalString(string? str)
    {
        if (str is null) return IntPtr.Zero;
        var bytes = Encoding.UTF8.GetBytes(str + '\0');
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }

    private static void FreeString(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
            Marshal.FreeHGlobal(ptr);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_windowHandle != IntPtr.Zero)
        {
            MacNative.WindowDestroy(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        _pinnedDelegates.Clear();
    }

    #endregion
}
