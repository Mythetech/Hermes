// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;

namespace Hermes.Platforms.Linux;

/// <summary>
/// Linux implementation of IHermesWindowBackend using native GTK3/WebKit2GTK library.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxWindowBackend : IHermesWindowBackend
{
    private static bool s_gtkInitialized;
    private static readonly object s_initLock = new();

    private IntPtr _windowHandle;
    private int _uiThreadId;
    private bool _initialized;
    private bool _disposed;

    // Keep strong references to delegates to prevent GC during window lifetime
    private readonly List<Delegate> _pinnedDelegates = new();

    // Custom scheme handlers registered before Initialize()
    private readonly Dictionary<string, Func<string, (Stream? Content, string? ContentType)>> _customSchemeHandlers = new();
    private LinuxNativeDelegates.CustomSchemeCallback? _customSchemeCallback;

    // Cached options for deferred initialization
    private string _pendingTitle = "Hermes Window";
    private int _pendingWidth = 800;
    private int _pendingHeight = 600;

    // Menu backend (lazily created)
    private LinuxMenuBackend? _menuBackend;

    // Native drag and drop, zero when the window did not opt in or the native library lacks it.
    private IntPtr _dragDrop;
    private IReadOnlyList<string> _dropCustomTypes = [];
    private readonly Dictionary<long, DropMessage> _pendingDropClaims = new();
    private long _nextDropClaimId;
    private TaskCompletionSource<HermesDragResult>? _dragCompletion;

    #region Lifecycle

    public void Initialize(HermesWindowOptions options)
    {
        if (_initialized)
            throw new InvalidOperationException("Window has already been initialized.");

        // Ensure GTK is initialized before creating any windows
        EnsureGtkInitialized();

        _uiThreadId = Environment.CurrentManagedThreadId;

        IsCustomTitleBarActive = options.CustomTitleBar;

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
            ContextMenuEnabled = options.ContextMenuEnabled,
            CustomTitleBar = options.CustomTitleBar,
            Transparent = options.Transparent
        };

        // Marshal strings
        parameters.Title = MarshalString(options.Title);
        parameters.StartUrl = MarshalString(options.StartUrl);
        parameters.StartHtml = MarshalString(options.StartHtml);
        parameters.IconPath = MarshalString(options.IconPath);

        // Initialize custom scheme names array (must be done before WindowCreate)
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
            _windowHandle = LinuxNative.WindowCreate(ref parameters);
            if (_windowHandle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create native window.");

            DragDropEnabled = options.DragDrop;
            if (DragDropEnabled)
                AttachDragDrop(options.DropCustomTypes);

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
        LinuxNative.WindowShow(_windowHandle);
    }

    public void Hide()
    {
        EnsureInitialized();
        LinuxNative.WindowHide(_windowHandle);
    }

    public void Close()
    {
        if (_initialized && _windowHandle != IntPtr.Zero)
        {
            LinuxNative.WindowClose(_windowHandle);
        }
    }

    public void WaitForClose()
    {
        EnsureInitialized();
        LinuxNative.WindowWaitForClose(_windowHandle);
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
                LinuxNative.WindowGetTitle(_windowHandle, buffer, 1024);
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
            LinuxNative.WindowSetTitle(_windowHandle, value);
        }
    }

    public (int Width, int Height) Size
    {
        get
        {
            if (!_initialized) return (_pendingWidth, _pendingHeight);

            LinuxNative.WindowGetSize(_windowHandle, out int width, out int height);
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
            LinuxNative.WindowSetSize(_windowHandle, value.Width, value.Height);
        }
    }

    public (int X, int Y) Position
    {
        get
        {
            EnsureInitialized();
            LinuxNative.WindowGetPosition(_windowHandle, out int x, out int y);
            return (x, y);
        }
        set
        {
            EnsureInitialized();
            LinuxNative.WindowSetPosition(_windowHandle, value.X, value.Y);
        }
    }

    public bool IsMaximized
    {
        get
        {
            if (!_initialized) return false;
            return LinuxNative.WindowGetIsMaximized(_windowHandle);
        }
        set
        {
            EnsureInitialized();
            var wasMaximized = LinuxNative.WindowGetIsMaximized(_windowHandle);
            LinuxNative.WindowSetIsMaximized(_windowHandle, value);

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
            if (!_initialized) return false;
            return LinuxNative.WindowGetIsMinimized(_windowHandle);
        }
        set
        {
            EnsureInitialized();
            LinuxNative.WindowSetIsMinimized(_windowHandle, value);
        }
    }

    public HermesPlatform Platform => HermesPlatform.Linux;

    /// <summary>
    /// Whether the window opted in to drag and drop; only then is the drop protocol kept from <see cref="WebMessageReceived"/>.
    /// </summary>
    internal bool DragDropEnabled { get; set; }

    public bool IsCustomTitleBarActive { get; private set; }

    #endregion

    #region WebView

    public void NavigateToUrl(string url)
    {
        EnsureInitialized();
        LinuxNative.WindowNavigateToUrl(_windowHandle, url);
    }

    public void NavigateToString(string html)
    {
        EnsureInitialized();
        LinuxNative.WindowNavigateToString(_windowHandle, html);
    }

    public void SendWebMessage(string message)
    {
        EnsureInitialized();
        LinuxNative.WindowSendWebMessage(_windowHandle, message);
    }

    public void RegisterCustomScheme(string scheme, Func<string, (Stream? Content, string? ContentType)> handler)
    {
        if (_initialized)
        {
            // After initialization, only allow updating handlers for pre-registered schemes
            if (!_customSchemeHandlers.ContainsKey(scheme))
            {
                throw new InvalidOperationException(
                    "Custom schemes must be registered before Initialize() is called. " +
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

        var invokeDelegate = new LinuxNativeDelegates.InvokeCallback(() =>
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
            LinuxNative.WindowInvoke(_windowHandle, Marshal.GetFunctionPointerForDelegate(invokeDelegate));
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
        GCHandle handle = default;
        var invokeDelegate = new LinuxNativeDelegates.InvokeCallback(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                HermesApplication.RaiseDispatcherUnhandledException(ex);
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        });

        handle = GCHandle.Alloc(invokeDelegate);
        LinuxNative.WindowBeginInvoke(_windowHandle, Marshal.GetFunctionPointerForDelegate(invokeDelegate));
    }

    #endregion

    #region Events

    public event Action? Closing;
    public event Action<int, int>? Resized;
    public event Action<int, int>? Moved;
    public event Action? FocusIn;
    public event Action? FocusOut;
    public event Action<string>? WebMessageReceived;
    public event Action<HermesDropReport>? DropReported;
    public event Action? Maximized;
    public event Action? Restored;
    internal event Action? PageLoaded;

    public Func<bool>? CloseRequestedHandler { get; set; }

    #endregion

    #region Drag and Drop

    public unsafe Task<HermesDragResult> StartDragAsync(HermesDataPackage package)
    {
        if (_dragDrop == IntPtr.Zero || _dragCompletion is not null)
            return Task.FromResult(HermesDragResult.Canceled);

        using var entries = new NativeTargetList(LinuxClipboardTargets.ForWrite(package));

        var completion = new TaskCompletionSource<HermesDragResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dragCompletion = completion;

        try
        {
            if (LinuxNative.DragDropBeginDrag(_dragDrop, entries.Targets, entries.Data, entries.Lengths, entries.Count))
                return completion.Task;
        }
        catch (EntryPointNotFoundException)
        {
        }
        catch
        {
            _dragCompletion = null;
            throw;
        }

        _dragCompletion = null;
        return Task.FromResult(HermesDragResult.Canceled);
    }

    private void AttachDragDrop(IReadOnlyList<string> customTypes)
    {
        _dropCustomTypes = customTypes;
        var claimedDelegate = new LinuxNativeDelegates.DropClaimedCallback(OnNativeDropClaimed);
        var endedDelegate = new LinuxNativeDelegates.DragEndedCallback(OnNativeDragEnded);
        _pinnedDelegates.Add(claimedDelegate);
        _pinnedDelegates.Add(endedDelegate);

        try
        {
            var dropTargets = LinuxClipboardTargets.DropTargets(customTypes);
            _dragDrop = LinuxNative.DragDropAttach(_windowHandle, DragDropScript.Source, [.. dropTargets], dropTargets.Count,
                Marshal.GetFunctionPointerForDelegate(claimedDelegate), Marshal.GetFunctionPointerForDelegate(endedDelegate));
        }
        catch (EntryPointNotFoundException ex)
        {
            // An older native library: no drop is captured and drags end as Canceled.
            Diagnostics.HermesLogger.Warning($"Native drag and drop is unavailable: {ex.Message}");
        }
    }

    private void ReceiveDropMessage(string message)
    {
        if (DropMessage.TryParse(message) is not { } drop)
            return;

        // Only the shim's capture can carry data, so without it the drop is reported empty and gets no ID.
        if (drop.Kind == HermesDropReportKind.Reset || _dragDrop == IntPtr.Zero)
        {
            RaiseDropReported(drop.ToReport(null));
            return;
        }

        var claimId = ++_nextDropClaimId;
        _pendingDropClaims[claimId] = drop;
        LinuxNative.DragDropClaim(_dragDrop, claimId);
    }

    private void OnNativeDropClaimed(long claimId, IntPtr targets, IntPtr data, IntPtr lengths, int count)
    {
        if (!_pendingDropClaims.Remove(claimId, out var drop))
            return;

        HermesDataPackage? package = null;
        try
        {
            if (count >= 0)
                package = LinuxClipboardTargets.ReadDrop(ReadTargets(targets, data, lengths, count), _dropCustomTypes);
        }
        catch (Exception ex)
        {
            // Nothing may unwind into the GTK main loop that called back.
            HermesApplication.RaiseDispatcherUnhandledException(ex);
            return;
        }

        RaiseDropReported(drop.ToReport(package));
    }

    private void RaiseDropReported(HermesDropReport report)
    {
        try
        {
            DropReported?.Invoke(report);
        }
        catch (Exception ex)
        {
            // A subscriber's exception goes to the dispatcher sink rather than out of the native callback.
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private void OnNativeDragEnded(bool dropped)
    {
        var completion = _dragCompletion;
        _dragCompletion = null;
        completion?.TrySetResult(dropped ? HermesDragResult.Dropped : HermesDragResult.Canceled);
    }

    private static List<(string Target, byte[] Data)> ReadTargets(IntPtr targets, IntPtr data, IntPtr lengths, int count)
    {
        var received = new List<(string Target, byte[] Data)>(count);
        for (var i = 0; i < count; i++)
        {
            var target = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(targets, i * IntPtr.Size));
            var bytes = new byte[Marshal.ReadInt32(lengths, i * sizeof(int))];
            Marshal.Copy(Marshal.ReadIntPtr(data, i * IntPtr.Size), bytes, 0, bytes.Length);
            if (target is not null)
                received.Add((target, bytes));
        }
        return received;
    }

    #endregion

    #region Factory Methods

    internal LinuxMenuBackend CreateMenuBackend()
    {
        EnsureInitialized();
        _menuBackend ??= new LinuxMenuBackend(_windowHandle, this);
        return _menuBackend;
    }

    internal LinuxDialogBackend CreateDialogBackend()
    {
        EnsureInitialized();
        return new LinuxDialogBackend(_windowHandle);
    }

    internal LinuxContextMenuBackend CreateContextMenuBackend()
    {
        EnsureInitialized();
        return new LinuxContextMenuBackend(_windowHandle);
    }

    #endregion

    #region Private Helpers

    private static void EnsureGtkInitialized()
    {
        if (s_gtkInitialized) return;

        lock (s_initLock)
        {
            if (s_gtkInitialized) return;

            // Initialize GTK via native library
            int argc = 0;
            IntPtr argv = IntPtr.Zero;
            LinuxNative.AppInit(ref argc, ref argv);
            RegisterAppId();

            s_gtkInitialized = true;
        }
    }

    // The portal Registry call must precede every other portal call, so it is made as soon as GTK is up.
    private static void RegisterAppId()
    {
        if (HermesApplication.AppId is not { } appId)
            return;

        try
        {
            LinuxNative.AppSetAppId(appId);
        }
        catch (EntryPointNotFoundException ex)
        {
            Diagnostics.HermesLogger.Warning($"The Hermes native library is out of date, so the AppId was not registered with the desktop portal: {ex.Message}");
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Window has not been initialized. Call Initialize() first.");
    }

    private void SetupCallbacks(ref HermesWindowParams parameters)
    {
        // Create delegates
        var closingDelegate = new LinuxNativeDelegates.ClosingCallback(OnNativeClosing);
        var resizedDelegate = new LinuxNativeDelegates.ResizedCallback(OnNativeResized);
        var movedDelegate = new LinuxNativeDelegates.MovedCallback(OnNativeMoved);
        var focusInDelegate = new LinuxNativeDelegates.FocusCallback(OnNativeFocusIn);
        var focusOutDelegate = new LinuxNativeDelegates.FocusCallback(OnNativeFocusOut);
        var webMessageDelegate = new LinuxNativeDelegates.WebMessageCallback(OnNativeWebMessage);

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
            _customSchemeCallback = new LinuxNativeDelegates.CustomSchemeCallback(OnNativeCustomScheme);
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

        // Set up WebView crash callback
        var webViewCrashDelegate = new LinuxNativeDelegates.WebViewCrashCallback(OnNativeWebViewCrash);
        _pinnedDelegates.Add(webViewCrashDelegate);
        parameters.OnWebViewCrash = Marshal.GetFunctionPointerForDelegate(webViewCrashDelegate);

        // Set up page loaded callback (used for JS accelerator injection)
        var pageLoadedDelegate = new LinuxNativeDelegates.PageLoadedCallback(OnNativePageLoaded);
        _pinnedDelegates.Add(pageLoadedDelegate);
        parameters.OnPageLoaded = Marshal.GetFunctionPointerForDelegate(pageLoadedDelegate);
    }

    private bool OnNativeClosing()
    {
        if (CloseRequestedHandler is not null)
        {
            var shouldClose = CloseRequestedHandler();
            if (!shouldClose) return false;
        }

        Closing?.Invoke();
        return true;
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

    private void OnNativeWebMessage(IntPtr messagePtr) => ReceiveWebMessage(Marshal.PtrToStringUTF8(messagePtr) ?? "");

    internal void ReceiveWebMessage(string message)
    {
        if (message.StartsWith("__hermes_accel:", StringComparison.Ordinal))
        {
            _menuBackend?.HandleAcceleratorMessage(message.Substring(15));
            return;
        }

        if (DragDropEnabled && DropMessage.IsDropProtocol(message))
        {
            ReceiveDropMessage(message);
            return;
        }

        WebMessageReceived?.Invoke(message);
    }

    private void OnNativePageLoaded() => PageLoaded?.Invoke();

    private void OnNativeWebViewCrash()
    {
        // Logged before the interceptor check so smoke mode records the crash as a hermes error and
        // ordinary apps get a logged error even without crash interception.
        Diagnostics.HermesLogger.Error("WebView content process terminated");

        if (!Diagnostics.HermesCrashInterceptor.IsEnabled)
            return;

        var context = Diagnostics.HermesCrashInterceptor.BuildCrashContext(
            new InvalidOperationException("WebKit web process terminated"),
            Contracts.Diagnostics.CrashSource.WebViewCrash);

        Diagnostics.HermesCrashInterceptor.NotifyCrash(context);
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
                var (stream, handlerContentType) = handler(url);
                if (stream is not null)
                {
                    using var ms = new MemoryStream();
                    using (stream)
                        stream.CopyTo(ms);
                    var data = ms.ToArray();

                    numBytes = data.Length;
                    var resultPtr = Marshal.AllocHGlobal(numBytes);
                    Marshal.Copy(data, 0, resultPtr, numBytes);

                    // Use Content-Type from handler if provided, otherwise fall back to extension-based detection
                    var contentType = handlerContentType ?? GetContentType(uri.AbsolutePath);
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
            ".webp" => "image/webp",
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

        if (_dragDrop != IntPtr.Zero)
        {
            LinuxNative.DragDropDetach(_dragDrop);
            _dragDrop = IntPtr.Zero;
        }
        OnNativeDragEnded(dropped: false);
        _pendingDropClaims.Clear();

        if (_windowHandle != IntPtr.Zero)
        {
            LinuxNative.WindowDestroy(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        _pinnedDelegates.Clear();
    }

    #endregion
}
