// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;

namespace Hermes.Contracts.Plugins;

/// <summary>
/// System-wide keyboard shortcuts that fire while the app is not focused.
/// </summary>
public interface IGlobalHotkeys
{
    /// <summary>Raised with the id of a registered hotkey when it is pressed.</summary>
    event Action<string>? Activated;

    /// <summary>
    /// Registers a hotkey under an app-defined id, in the menu accelerator syntax such as "Ctrl+Shift+K".
    /// Ctrl means Command on macOS, and Meta means the Control key there.
    /// </summary>
    /// <remarks>Await it rather than blocking on it: completing it can need the app's message loop, so a wait on the UI thread, before the loop runs, or in an <see cref="Activated"/> handler may never return.</remarks>
    Task<HermesHotkeyResult> RegisterAsync(string id, string accelerator, string? description = null);

    /// <summary>
    /// Registers a hotkey under an app-defined id, in the menu accelerator syntax such as "Ctrl+Shift+K".
    /// Ctrl means Command on macOS, and Meta means the Control key there.
    /// </summary>
    /// <remarks>Await it rather than blocking on it: completing it can need the app's message loop, so a wait on the UI thread, before the loop runs, or in an <see cref="Activated"/> handler may never return. Canceling before it completes leaves the hotkey unregistered and throws <see cref="OperationCanceledException"/>; on Wayland it also withdraws a consent dialog that is still open.</remarks>
    Task<HermesHotkeyResult> RegisterAsync(string id, string accelerator, string? description, CancellationToken ct);

    /// <summary>Unregisters a hotkey; unknown ids are ignored.</summary>
    Task UnregisterAsync(string id);

    /// <summary>Unregisters a hotkey; unknown ids are ignored.</summary>
    /// <remarks>The token stops the wait for an earlier hotkey call to finish; once the hotkey is being released, it completes.</remarks>
    Task UnregisterAsync(string id, CancellationToken ct);
}
