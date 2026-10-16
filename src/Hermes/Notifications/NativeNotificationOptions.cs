// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Reflection;

namespace Hermes.Notifications;

/// <summary>
/// Identity used when registering the app with the platform notification system.
/// Pass to <see cref="HermesApplication.ConfigureNotifications"/> before the first use of
/// <see cref="HermesApplication.Notifications"/>.
/// </summary>
public sealed class NativeNotificationOptions
{
    /// <summary>Stable application identifier. Defaults to <see cref="HermesApplication.AppId"/> when it is set, otherwise the entry assembly name.</summary>
    public string? AppId { get; set; }

    /// <summary>Name shown by the platform as the notification source. Defaults to <see cref="AppId"/>.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Absolute path to the app icon used as the notification source icon.</summary>
    public string? IconPath { get; set; }

    internal NativeNotificationOptions Resolve() => Resolve(HermesApplication.AppId);

    internal NativeNotificationOptions Resolve(string? applicationAppId)
    {
        var appId = !string.IsNullOrWhiteSpace(AppId) ? AppId
            : !string.IsNullOrWhiteSpace(applicationAppId) ? applicationAppId
            : Assembly.GetEntryAssembly()?.GetName().Name ?? "HermesApp";

        return new NativeNotificationOptions
        {
            AppId = appId,
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? appId : DisplayName,
            IconPath = IconPath,
        };
    }
}
