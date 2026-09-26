// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Diagnostics;
using Hermes.Notifications;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using static Hermes.Platforms.Windows.WinRtInterop;

namespace Hermes.Platforms.Windows;

/// <summary>
/// Windows implementation of <see cref="INotificationBackend"/> using WinRT toast notifications.
/// Registers the app's AppUserModelID under HKCU so an unpackaged exe may toast, and marshals
/// Activated callbacks (WinRT thread pool) onto the UI thread through a message-only window,
/// mirroring <see cref="WindowsStatusIconBackend"/>.
/// <para>
/// ToastNotifier and the toast objects are apartment-bound: created on the STA UI thread they reject
/// calls from thread-pool threads with RPC_E_WRONG_THREAD, and a Blazor app calls ShowAsync from both.
/// Every WinRT object therefore lives in the MTA, which all thread-pool threads share, and every WinRT
/// call is routed there through <see cref="RunInMultithreadedApartment"/>. Only the message-only window
/// stays on the constructing UI thread, because click delivery needs that thread's pump.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsNotificationBackend : INotificationBackend
{
    private const string WindowClassName = "HermesNotificationWindow";
    private const uint WM_NOTIFICATION_CLICKED = PInvoke.WM_APP + 2;
    private const string ToastGroup = "hermes";
    private const int NotificationSettingEnabled = 0;
    private const int TrackedToastCapacity = 256;

    // HWND_MESSAGE is a macro cast ((HWND)-3) in winuser.h, which CsWin32 cannot emit as a constant.
    private static readonly HWND s_messageOnlyParent = new(-3);

    private static readonly WNDPROC s_wndProc = NotificationWindowProc;
    private static readonly object s_registrationLock = new();
    private static readonly Dictionary<HWND, WindowsNotificationBackend> s_hwndToInstance = new();
    private static bool s_classRegistered;
    private static HINSTANCE s_hInstance;

    private readonly string _appId;
    private readonly ConcurrentQueue<string> _pendingClicks = new();
    private readonly Dictionary<string, (IntPtr Toast, long Token)> _toastsById = new();
    private readonly Queue<string> _toastOrder = new();
    private readonly object _toastsLock = new();
    private HWND _hwnd;
    private IntPtr _notifier;
    private IntPtr _toastFactory;
    private IntPtr _history;
    private bool _disposed;
    private bool _settingProbed;

    public event Action<string>? Clicked;

    public bool IsSupported { get; }

    public string? UnsupportedReason { get; }

    internal WindowsNotificationBackend(NativeNotificationOptions options)
    {
        _appId = NotificationAppId.Sanitize(options.AppId ?? "HermesApp");

        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            UnsupportedReason = "toast notifications require Windows 10 or later";
            return;
        }

        try
        {
            RegisterAppUserModelId(_appId, options.DisplayName ?? _appId, options.IconPath);
            RunInMultithreadedApartment(CreateWinRtObjects);
            CreateMessageWindow();
            ToastActivatedHandler.Activated = OnToastActivated;
            IsSupported = true;
        }
        catch (Exception ex)
        {
            UnsupportedReason = $"toast notifications unavailable: {ex.Message}";
            ReleaseNativeResources();
        }
    }

    public Task<bool> RequestPermissionAsync()
    {
        EnsureNotDisposed();
        return Task.FromResult(IsSupported);
    }

    public Task ShowAsync(string id, string title, string? body, string? iconPath, bool silent)
    {
        EnsureNotDisposed();

        if (!IsSupported)
            return Task.FromException(new InvalidOperationException($"Toast notifications are unavailable: {UnsupportedReason}"));

        return Task.Run(() => ShowCore(id, title, body, iconPath, silent));
    }

    private void ShowCore(string id, string title, string? body, string? iconPath, bool silent)
    {
        try
        {
            var toast = CreateToast(ToastXmlBuilder.Build(id, title, body, iconPath, silent));
            long token;
            try
            {
                var toast2 = QueryInterface(toast, in IID_IToastNotification2);
                try
                {
                    using var tag = new HString(ToastTag.FromId(id));
                    using var group = new HString(ToastGroup);
                    SetTag(toast2, tag.Handle);
                    SetGroup(toast2, group.Handle);
                }
                finally
                {
                    Release(toast2);
                }

                token = AddActivatedHandler(toast, ToastActivatedHandler.Instance);
            }
            catch
            {
                Release(toast);
                throw;
            }

            lock (_toastsLock)
            {
                var replaced = _toastsById.Remove(id, out var previous);
                if (replaced)
                    ReleaseToast(previous);
                _toastsById[id] = (toast, token);
                if (!replaced)
                    _toastOrder.Enqueue(id);

                // An evicted toast stays on screen and only loses its click callback, which matches the
                // 256-entry tag map the notification center itself keeps.
                while (_toastOrder.Count > TrackedToastCapacity)
                {
                    var evicted = _toastOrder.Dequeue();
                    if (_toastsById.Remove(evicted, out var old))
                        ReleaseToast(old);
                }
            }

            ShowToast(_notifier, toast);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Windows rejected the notification for app id '{_appId}': {ex.Message}", ex);
        }

        ProbeSettingAfterFirstShow();
    }

    // Unpackaged apps cannot read ToastNotifier.Setting (or use the history API) until they have sent
    // one toast; Windows answers ERROR_NOT_FOUND before that, which the first Windows run hit when the
    // probe ran ahead of Show. The Community Toolkit works around the same rule with a hidden toast.
    // Reading it once after a successful Show keeps the "toasts are disabled" hint without that cost.
    private void ProbeSettingAfterFirstShow()
    {
        if (_settingProbed)
            return;

        _settingProbed = true;
        try
        {
            var setting = GetNotifierSetting(_notifier);
            if (setting != NotificationSettingEnabled)
                HermesLogger.Info($"Toast notifications are disabled for '{_appId}' (setting {setting}); toasts are queued but will not display.");
        }
        catch (Exception ex)
        {
            HermesLogger.Info($"Could not read the toast notification setting for '{_appId}': {ex.Message}");
        }
    }


    public void Dismiss(string id)
    {
        EnsureNotDisposed();

        if (!IsSupported)
            return;

        RunInMultithreadedApartment(() => DismissCore(id));
    }

    private void DismissCore(string id)
    {
        lock (_toastsLock)
        {
            if (_toastsById.Remove(id, out var entry))
                ReleaseToast(entry);
        }

        if (_history == IntPtr.Zero)
            return;

        try
        {
            using var tag = new HString(ToastTag.FromId(id));
            using var group = new HString(ToastGroup);
            using var appId = new HString(_appId);
            RemoveGroupedTagWithId(_history, tag.Handle, group.Handle, appId.Handle);
        }
        catch (Exception ex)
        {
            // The history API is subject to the same send-first rule as Setting (see ProbeSettingAfterFirstShow),
            // and a dismiss that finds nothing to remove is not an error for the caller.
            HermesLogger.Info($"Could not remove toast '{id}' from the notification history: {ex.Message}");
        }
    }

    public void DismissAll()
    {
        EnsureNotDisposed();

        if (!IsSupported)
            return;

        RunInMultithreadedApartment(DismissAllCore);
    }

    private void DismissAllCore()
    {
        lock (_toastsLock)
        {
            foreach (var entry in _toastsById.Values)
                ReleaseToast(entry);
            _toastsById.Clear();
            _toastOrder.Clear();
        }

        if (_history == IntPtr.Zero)
            return;

        try
        {
            using var appId = new HString(_appId);
            ClearWithId(_history, appId.Handle);
        }
        catch (Exception ex)
        {
            HermesLogger.Info($"Could not clear the notification history for '{_appId}': {ex.Message}");
        }
    }

    #region Toast creation

    private void CreateWinRtObjects()
    {
        var managerStatics = GetActivationFactory(ToastNotificationManagerClass, in IID_IToastNotificationManagerStatics);
        try
        {
            using var appId = new HString(_appId);
            _notifier = CreateToastNotifierWithId(managerStatics, appId.Handle);
            _history = TryGetHistory(managerStatics);
        }
        finally
        {
            Release(managerStatics);
        }

        _toastFactory = GetActivationFactory(ToastNotificationClass, in IID_IToastNotificationFactory);
    }

    // .NET thread-pool threads are MTA, so a blocking join on a pool task is the cheapest way to reach
    // that apartment from the STA UI thread. The join never depends on the UI pump, which keeps it safe
    // to call from Dispose after the message loop has exited.
    private static void RunInMultithreadedApartment(Action action) =>
        Task.Run(action).GetAwaiter().GetResult();

    private IntPtr CreateToast(string xml)
    {
        var document = ActivateInstance(XmlDocumentClass);
        try
        {
            var documentIo = QueryInterface(document, in IID_IXmlDocumentIO);
            try
            {
                using var xmlString = new HString(xml);
                LoadXml(documentIo, xmlString.Handle);
            }
            finally
            {
                Release(documentIo);
            }

            var xmlDocument = QueryInterface(document, in IID_IXmlDocument);
            try
            {
                return CreateToastNotification(_toastFactory, xmlDocument);
            }
            finally
            {
                Release(xmlDocument);
            }
        }
        finally
        {
            Release(document);
        }
    }

    private static IntPtr TryGetHistory(IntPtr managerStatics)
    {
        IntPtr statics2;
        try
        {
            statics2 = QueryInterface(managerStatics, in IID_IToastNotificationManagerStatics2);
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }

        try
        {
            return GetHistory(statics2);
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
        finally
        {
            Release(statics2);
        }
    }

    private static void ReleaseToast((IntPtr Toast, long Token) entry)
    {
        RemoveActivatedHandler(entry.Toast, entry.Token);
        Release(entry.Toast);
    }

    #endregion

    #region Registration

    private static void RegisterAppUserModelId(string appId, string displayName, string? iconPath)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{appId}");
        key.SetValue("DisplayName", displayName);

        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            key.SetValue("IconUri", iconPath);
        else
            key.DeleteValue("IconUri", throwOnMissingValue: false);
    }

    #endregion

    #region Click marshaling

    private void OnToastActivated(string id)
    {
        if (_disposed || _hwnd.IsNull)
            return;

        _pendingClicks.Enqueue(id);
        PInvoke.PostMessage(_hwnd, WM_NOTIFICATION_CLICKED, 0, 0);
    }

    private void CreateMessageWindow()
    {
        EnsureWindowClassRegistered();

        unsafe
        {
            fixed (char* className = WindowClassName)
            {
                _hwnd = PInvoke.CreateWindowEx(
                    0,
                    className,
                    className,
                    0,
                    0, 0, 0, 0,
                    s_messageOnlyParent,
                    HMENU.Null,
                    s_hInstance,
                    null);
            }
        }

        if (_hwnd.IsNull)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create notification message window");

        lock (s_registrationLock)
        {
            s_hwndToInstance[_hwnd] = this;
        }
    }

    private static void EnsureWindowClassRegistered()
    {
        if (s_classRegistered) return;

        lock (s_registrationLock)
        {
            if (s_classRegistered) return;

            s_hInstance = PInvoke.GetModuleHandle((PCWSTR)null);

            unsafe
            {
                fixed (char* className = WindowClassName)
                {
                    var wcx = new WNDCLASSEXW
                    {
                        cbSize = (uint)sizeof(WNDCLASSEXW),
                        lpfnWndProc = s_wndProc,
                        hInstance = s_hInstance,
                        lpszClassName = className
                    };

                    if (PInvoke.RegisterClassEx(in wcx) == 0)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to register notification window class");
                }
            }

            s_classRegistered = true;
        }
    }

    private static LRESULT NotificationWindowProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        WindowsNotificationBackend? instance;
        lock (s_registrationLock)
        {
            s_hwndToInstance.TryGetValue(hwnd, out instance);
        }

        if (instance is null || uMsg != WM_NOTIFICATION_CLICKED)
            return PInvoke.DefWindowProc(hwnd, uMsg, wParam, lParam);

        instance.DrainClicks();
        return new LRESULT(0);
    }

    private void DrainClicks()
    {
        while (_pendingClicks.TryDequeue(out var id))
            Clicked?.Invoke(id);
    }

    #endregion

    #region Disposal

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ReleaseNativeResources()
    {
        if (IsSupported)
            ToastActivatedHandler.Activated = null;

        RunInMultithreadedApartment(ReleaseWinRtObjects);

        if (!_hwnd.IsNull)
        {
            lock (s_registrationLock)
            {
                s_hwndToInstance.Remove(_hwnd);
            }
            PInvoke.DestroyWindow(_hwnd);
            _hwnd = HWND.Null;
        }
    }

    private void ReleaseWinRtObjects()
    {
        lock (_toastsLock)
        {
            foreach (var entry in _toastsById.Values)
                ReleaseToast(entry);
            _toastsById.Clear();
            _toastOrder.Clear();
        }

        Release(_history);
        _history = IntPtr.Zero;
        Release(_toastFactory);
        _toastFactory = IntPtr.Zero;
        Release(_notifier);
        _notifier = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ToastActivatedHandler.Activated = null;
        ReleaseNativeResources();
    }

    #endregion
}
#endif
