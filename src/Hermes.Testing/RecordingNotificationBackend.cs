// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;

namespace Hermes.Testing;

/// <summary>
/// A fake notification backend that records operations for verification in tests.
/// </summary>
public sealed class RecordingNotificationBackend : INotificationBackend
{
    private readonly List<string> _operations = new();
    private readonly List<(string Id, string Title, string? Body, string? IconPath, bool Silent)> _shown = new();
    private readonly List<string> _dismissed = new();

    public event Action<string>? Clicked;

    public IReadOnlyList<string> Operations => _operations;

    public IReadOnlyList<(string Id, string Title, string? Body, string? IconPath, bool Silent)> Shown => _shown;

    public IReadOnlyList<string> Dismissed => _dismissed;

    public int DismissAllCount { get; private set; }

    public int PermissionRequests { get; private set; }

    public bool IsSupported { get; set; } = true;

    public string? UnsupportedReason { get; set; }

    public bool PermissionResult { get; set; } = true;

    /// <summary>When set, <see cref="ShowAsync"/> throws this instead of recording.</summary>
    public Exception? ShowException { get; set; }

    public bool IsDisposed { get; private set; }

    public Task<bool> RequestPermissionAsync()
    {
        PermissionRequests++;
        _operations.Add("RequestPermission");
        return Task.FromResult(PermissionResult);
    }

    public Task ShowAsync(string id, string title, string? body, string? iconPath, bool silent)
    {
        if (ShowException is not null)
            return Task.FromException(ShowException);

        _shown.Add((id, title, body, iconPath, silent));
        _operations.Add($"Show:{id}");
        return Task.CompletedTask;
    }

    public void Dismiss(string id)
    {
        _dismissed.Add(id);
        _operations.Add($"Dismiss:{id}");
    }

    public void DismissAll()
    {
        DismissAllCount++;
        _operations.Add("DismissAll");
    }

    public void RaiseClicked(string id) => Clicked?.Invoke(id);

    public void Dispose()
    {
        IsDisposed = true;
        _operations.Add("Dispose");
    }
}
