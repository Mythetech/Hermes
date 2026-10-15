// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;
using Hermes.Menu;

namespace Hermes.Abstractions;

/// <summary>
/// Platform half of global hotkeys. Receives only validated, unique registrations from <see cref="GlobalHotkeys"/>.
/// </summary>
internal interface IGlobalHotkeyBackend
{
    bool IsSupported { get; }

    string? UnsupportedReason { get; }

    /// <summary>Registers one accelerator whose key is in canonical form; completes with the platform's result.</summary>
    Task<HermesHotkeyResult> RegisterAsync(string id, Accelerator accelerator, string description);

    Task UnregisterAsync(string id);

    /// <summary>Raised on the platform's thread with the id of a pressed hotkey.</summary>
    event Action<string>? Activated;
}
