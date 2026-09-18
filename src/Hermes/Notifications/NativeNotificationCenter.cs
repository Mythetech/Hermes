// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Notifications;
using Hermes.Diagnostics;

namespace Hermes.Notifications;

/// <summary>
/// Posts native OS notifications and reports clicks. Obtain via <see cref="HermesApplication.Notifications"/>.
/// On hosts that cannot notify, <see cref="IsSupported"/> is false and <see cref="ShowAsync"/> logs and returns
/// instead of throwing, so apps never need to branch on platform support.
/// </summary>
public sealed class NativeNotificationCenter : IDisposable
{
    internal const int TagMapCapacity = 256;

    private readonly INotificationBackend _backend;
    private readonly Action<string> _warn;
    private readonly Dictionary<string, string?> _tagsById = new();
    private readonly Queue<string> _tagOrder = new();
    private readonly object _tagLock = new();
    private bool _permissionRequested;
    private bool _permissionGranted;
    private bool _unsupportedWarned;
    private bool _deniedWarned;
    private bool _disposed;

    internal NativeNotificationCenter(INotificationBackend backend, Action<string>? warningSink = null)
    {
        _backend = backend;
        _warn = warningSink ?? HermesLogger.Warning;
        _backend.Clicked += OnBackendClicked;
    }

    /// <summary>Whether this host can show notifications.</summary>
    public bool IsSupported => _backend.IsSupported;

    /// <summary>Why <see cref="IsSupported"/> is false, or null.</summary>
    public string? UnsupportedReason => _backend.UnsupportedReason;

    /// <summary>Raised on the UI thread when the user clicks a notification.</summary>
    public event Action<NotificationClickedEventArgs>? Clicked;

    /// <summary>
    /// Requests permission where the platform has a prompt (macOS). Always forwards to the platform so
    /// an app can re-check after the user changes system settings. Returns false on unsupported hosts.
    /// </summary>
    public async Task<bool> RequestPermissionAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!_backend.IsSupported)
            return false;

        ct.ThrowIfCancellationRequested();
        _permissionGranted = await _backend.RequestPermissionAsync();
        _permissionRequested = true;
        return _permissionGranted;
    }

    /// <summary>
    /// Shows a notification. Completes when the platform accepted the request. Requests permission first
    /// if the app never did. Never throws for lack of platform support or denied permission; those are logged once.
    /// </summary>
    /// <exception cref="ArgumentException">Title is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">IconPath does not exist.</exception>
    /// <exception cref="InvalidOperationException">The platform rejected the notification.</exception>
    public async Task ShowAsync(NativeNotification notification, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(notification);
        if (string.IsNullOrWhiteSpace(notification.Title))
            throw new ArgumentException("Notification title must not be empty.", nameof(notification));
        if (notification.IconPath is not null && !File.Exists(notification.IconPath))
            throw new FileNotFoundException("Notification icon was not found.", notification.IconPath);

        if (!_backend.IsSupported)
        {
            WarnOnce(ref _unsupportedWarned,
                $"Native notifications are not available on this host ({_backend.UnsupportedReason}); notifications will be skipped.");
            return;
        }

        ct.ThrowIfCancellationRequested();
        if (!_permissionRequested)
            await RequestPermissionAsync(ct);

        if (!_permissionGranted)
        {
            WarnOnce(ref _deniedWarned, "Notification permission was not granted; notifications will be skipped.");
            return;
        }

        Remember(notification.Id, notification.Tag);
        await _backend.ShowAsync(notification.Id, notification.Title, notification.Body, notification.IconPath, notification.Silent);
    }

    /// <summary>Removes a delivered or pending notification by id.</summary>
    public void Dismiss(string id)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _backend.Dismiss(id);
    }

    /// <summary>Removes every notification this app has delivered.</summary>
    public void DismissAll()
    {
        ThrowIfDisposed();
        _backend.DismissAll();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _backend.Clicked -= OnBackendClicked;
        _backend.Dispose();
    }

    private void OnBackendClicked(string id)
    {
        if (_disposed)
            return;

        string? tag;
        lock (_tagLock)
        {
            _tagsById.TryGetValue(id, out tag);
        }

        var handler = Clicked;
        if (handler is null)
            return;

        try
        {
            handler(new NotificationClickedEventArgs(id, tag));
        }
        catch (Exception ex)
        {
            HermesApplication.RaiseDispatcherUnhandledException(ex);
        }
    }

    private void Remember(string id, string? tag)
    {
        lock (_tagLock)
        {
            if (_tagsById.ContainsKey(id))
            {
                _tagsById[id] = tag;
                return;
            }

            _tagsById[id] = tag;
            _tagOrder.Enqueue(id);
            while (_tagOrder.Count > TagMapCapacity)
                _tagsById.Remove(_tagOrder.Dequeue());
        }
    }

    private void WarnOnce(ref bool flag, string message)
    {
        if (flag)
            return;

        flag = true;
        _warn(message);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
