// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.Hotkeys;
using Hermes.Menu;

namespace Hermes.Hotkeys;

/// <summary>
/// Backend for platforms and desktops with no global hotkeys.
/// </summary>
internal sealed class UnsupportedGlobalHotkeyBackend(string reason) : IGlobalHotkeyBackend
{
    public bool IsSupported => false;

    public string? UnsupportedReason => reason;

    public Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description, CancellationToken ct) =>
        Task.FromResult(new HermesHotkeyResult(HermesHotkeyStatus.Unsupported, null, reason));

    public Task UnregisterAsync(string id) => Task.CompletedTask;

    public event Action<string>? Activated
    {
        add { }
        remove { }
    }
}
