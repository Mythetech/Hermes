// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;

namespace Hermes.Notifications;

/// <summary>
/// Backend used when the current host cannot show notifications. Every operation is a no-op
/// so callers never have to branch on platform support.
/// </summary>
internal sealed class UnsupportedNotificationBackend : INotificationBackend
{
    public UnsupportedNotificationBackend(string reason)
    {
        UnsupportedReason = reason;
    }

    public bool IsSupported => false;

    public string? UnsupportedReason { get; }

#pragma warning disable CS0067
    public event Action<string>? Clicked;
#pragma warning restore CS0067

    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public Task ShowAsync(string id, string title, string? body, string? iconPath, bool silent) => Task.CompletedTask;

    public void Dismiss(string id)
    {
    }

    public void DismissAll()
    {
    }

    public void Dispose()
    {
    }
}
