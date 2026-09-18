// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Hermes.Abstractions;
using Hermes.Notifications;

namespace Hermes.Platforms.Linux;

/// <summary>
/// Linux implementation of <see cref="INotificationBackend"/> over org.freedesktop.Notifications via GDBus.
/// Completion callbacks and clicks arrive on the GLib main context, which is the UI thread.
/// TaskCompletionSources travel through the native context pointer as GCHandles.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxNotificationBackend : INotificationBackend
{
    private static readonly LinuxNativeDelegates.NotificationPermissionCallback s_permissionCallback = OnNativePermission;
    private static readonly LinuxNativeDelegates.NotificationCompletionCallback s_completionCallback = OnNativeCompletion;

    private readonly LinuxNativeDelegates.NotificationClickedCallback _clickCallback;
    private IntPtr _handle;
    private bool _disposed;

    public event Action<string>? Clicked;

    internal LinuxNotificationBackend(NativeNotificationOptions options)
    {
        _clickCallback = OnNativeClicked;
        _handle = LinuxNative.NotificationsCreate(
            options.DisplayName ?? options.AppId ?? "Hermes",
            options.IconPath,
            Marshal.GetFunctionPointerForDelegate(_clickCallback));

        IsSupported = LinuxNative.NotificationsIsSupported(_handle, out var reasonPtr);
        UnsupportedReason = IsSupported ? null : Marshal.PtrToStringUTF8(reasonPtr);
    }

    public bool IsSupported { get; }

    public string? UnsupportedReason { get; }

    public Task<bool> RequestPermissionAsync()
    {
        EnsureNotDisposed();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(completion));
        LinuxNative.NotificationsRequestPermission(_handle, Marshal.GetFunctionPointerForDelegate(s_permissionCallback), context);
        return completion.Task;
    }

    public Task ShowAsync(string id, string title, string? body, string? iconPath, bool silent)
    {
        EnsureNotDisposed();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = GCHandle.ToIntPtr(GCHandle.Alloc(completion));
        LinuxNative.NotificationsShow(_handle, id, title, body, iconPath, silent,
            Marshal.GetFunctionPointerForDelegate(s_completionCallback), context);
        return completion.Task;
    }

    public void Dismiss(string id)
    {
        EnsureNotDisposed();
        LinuxNative.NotificationsDismiss(_handle, id);
    }

    public void DismissAll()
    {
        EnsureNotDisposed();
        LinuxNative.NotificationsDismissAll(_handle);
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
        completion.TrySetException(new InvalidOperationException($"Notification daemon rejected the notification: {message}"));
    }

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            LinuxNative.NotificationsDestroy(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~LinuxNotificationBackend()
    {
        Dispose();
    }
}
