// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific backend for native notifications. Backends deal in notification ids only;
/// tags are resolved by <see cref="Hermes.Notifications.NativeNotificationCenter"/>.
/// </summary>
internal interface INotificationBackend : IDisposable
{
    bool IsSupported { get; }

    string? UnsupportedReason { get; }

    Task<bool> RequestPermissionAsync();

    Task ShowAsync(string id, string title, string? body, string? iconPath, bool silent);

    void Dismiss(string id);

    void DismissAll();

    /// <summary>Raised on the UI thread with the id of the clicked notification.</summary>
    event Action<string>? Clicked;
}
