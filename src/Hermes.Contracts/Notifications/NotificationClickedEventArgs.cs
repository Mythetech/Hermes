// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.Notifications;

/// <summary>
/// Raised when the user clicks a notification. <paramref name="Tag"/> is the value the app
/// supplied on <see cref="NativeNotification.Tag"/>, or null when unknown.
/// </summary>
public sealed record NotificationClickedEventArgs(string Id, string? Tag);
