// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Notifications;
using Hermes.Contracts.Plugins;

namespace Hermes.Plugins;

/// <summary>
/// Desktop implementation of <see cref="INativeNotifications"/> that forwards to
/// <see cref="HermesApplication.Notifications"/>. The center is resolved on every call rather than
/// captured, so a center recreated after <see cref="HermesApplication.Shutdown"/> is picked up.
/// </summary>
public sealed class DesktopNativeNotifications : INativeNotifications
{
    public bool IsSupported => HermesApplication.Notifications.IsSupported;

    public string? UnsupportedReason => HermesApplication.Notifications.UnsupportedReason;

    public event Action<NotificationClickedEventArgs>? Clicked
    {
        add => HermesApplication.Notifications.Clicked += value;
        remove => HermesApplication.Notifications.Clicked -= value;
    }

    public Task<bool> RequestPermissionAsync(CancellationToken ct = default)
        => HermesApplication.Notifications.RequestPermissionAsync(ct);

    public Task ShowAsync(NativeNotification notification, CancellationToken ct = default)
        => HermesApplication.Notifications.ShowAsync(notification, ct);

    public void Dismiss(string id) => HermesApplication.Notifications.Dismiss(id);

    public void DismissAll() => HermesApplication.Notifications.DismissAll();
}
