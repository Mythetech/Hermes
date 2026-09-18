// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Notifications;

/// <summary>
/// Describes a native OS notification. The <see cref="Id"/> is generated up front so a caller
/// can dismiss the notification later without waiting for it to be shown.
/// </summary>
public sealed class NativeNotification
{
    /// <summary>Unique identifier. Defaults to a compact GUID.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Headline text. Required and must not be whitespace.</summary>
    public required string Title { get; init; }

    /// <summary>Secondary text under the title.</summary>
    public string? Body { get; init; }

    /// <summary>
    /// App-defined payload returned in <see cref="NotificationClickedEventArgs.Tag"/> when the
    /// notification is clicked. Typically a route or entity key.
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>Absolute path to a PNG shown with the notification. Null uses the app icon.</summary>
    public string? IconPath { get; init; }

    /// <summary>Suppresses the platform notification sound.</summary>
    public bool Silent { get; init; }
}
