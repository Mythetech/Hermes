// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Notifications;

namespace Hermes.Contracts.Plugins;

/// <summary>
/// DI-friendly access to native OS notifications.
/// </summary>
public interface INativeNotifications
{
    /// <summary>Whether this host can show notifications. False on unbundled macOS processes and Linux without a daemon.</summary>
    bool IsSupported { get; }

    /// <summary>Why <see cref="IsSupported"/> is false, or null.</summary>
    string? UnsupportedReason { get; }

    /// <summary>Requests permission where the platform has a prompt (macOS). Returns true elsewhere.</summary>
    Task<bool> RequestPermissionAsync(CancellationToken ct = default);

    /// <summary>Shows a notification. Completes when the platform accepted it; never throws for lack of support.</summary>
    Task ShowAsync(NativeNotification notification, CancellationToken ct = default);

    /// <summary>Removes a delivered or pending notification by id.</summary>
    void Dismiss(string id);

    /// <summary>Removes every notification this app has delivered.</summary>
    void DismissAll();

    /// <summary>Raised on the UI thread when the user clicks a notification.</summary>
    event Action<NotificationClickedEventArgs>? Clicked;
}
