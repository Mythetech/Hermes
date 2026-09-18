// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Notifications;

namespace Hermes.Platforms.macOS;

/// <summary>
/// macOS implementation of <see cref="INotificationBackend"/> over UNUserNotificationCenter.
/// Completion callbacks arrive on framework queues and complete TaskCompletionSources whose
/// GCHandles travel through the native context pointer. Clicks arrive on the main thread.
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacNotificationBackend : INotificationBackend
{
    private static readonly MacNativeDelegates.NotificationPermissionCallback s_permissionCallback = OnNativePermission;
    private static readonly MacNativeDelegates.NotificationCompletionCallback s_completionCallback = OnNativeCompletion;

    private readonly MacNativeDelegates.NotificationClickedCallback _clickCallback;
    private IntPtr _handle;
    private bool _disposed;

    public event Action<string>? Clicked;

    internal MacNotificationBackend(NativeNotificationOptions options)
    {
        _clickCallback = OnNativeClicked;
        _handle = MacNative.NotificationsCreate(
            options.AppId ?? "Hermes",
            options.IconPath,
            Marshal.GetFunctionPointerForDelegate(_clickCallback));

        IsSupported = MacNative.NotificationsIsSupported(_handle, out var reasonPtr);
        UnsupportedReason = IsSupported ? null : Marshal.PtrToStringUTF8(reasonPtr);
    }

    public bool IsSupported { get; }

    public string? UnsupportedReason { get; }

    public Task<bool> RequestPermissionAsync()
    {
        EnsureNotDisposed();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(completion));
        MacNative.NotificationsRequestPermission(_handle, Marshal.GetFunctionPointerForDelegate(s_permissionCallback), context);
        return completion.Task;
    }

    public Task ShowAsync(string id, string title, string? body, string? iconPath, bool silent)
    {
        EnsureNotDisposed();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(completion));
        MacNative.NotificationsShow(_handle, id, title, body, iconPath, silent,
            Marshal.GetFunctionPointerForDelegate(s_completionCallback), context);
        return completion.Task;
    }

    public void Dismiss(string id)
    {
        EnsureNotDisposed();
        MacNative.NotificationsDismiss(_handle, id);
    }

    public void DismissAll()
    {
        EnsureNotDisposed();
        MacNative.NotificationsDismissAll(_handle);
    }

    private void OnNativeClicked(IntPtr notificationIdPtr)
    {
        var id = Marshal.PtrToStringUTF8(notificationIdPtr);
        if (id is not null)
            Clicked?.Invoke(id);
    }

    private static void OnNativePermission(IntPtr context, bool granted)
    {
        var handle = GCHandle.FromIntPtr(context);
        var completion = (TaskCompletionSource<bool>)handle.Target!;
        handle.Free();
        completion.TrySetResult(granted);
    }

    private static void OnNativeCompletion(IntPtr context, IntPtr errorPtr)
    {
        var handle = GCHandle.FromIntPtr(context);
        var completion = (TaskCompletionSource)handle.Target!;
        handle.Free();

        if (errorPtr == IntPtr.Zero)
        {
            completion.TrySetResult();
            return;
        }

        var message = Marshal.PtrToStringUTF8(errorPtr) ?? "unknown error";
        completion.TrySetException(new InvalidOperationException($"macOS rejected the notification: {message}"));
    }

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            MacNative.NotificationsDestroy(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~MacNotificationBackend()
    {
        Dispose();
    }
}
