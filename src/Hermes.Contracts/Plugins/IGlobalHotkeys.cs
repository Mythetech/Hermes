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
    Task<HermesHotkeyResult> RegisterAsync(string id, string accelerator, string? description = null);

    /// <summary>Unregisters a hotkey; unknown ids are ignored.</summary>
    Task UnregisterAsync(string id);
}
