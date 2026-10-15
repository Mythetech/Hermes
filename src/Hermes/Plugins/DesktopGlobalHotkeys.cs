// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.Hotkeys;
using Hermes.Contracts.Plugins;

namespace Hermes.Plugins;

/// <summary>
/// Desktop implementation of <see cref="IGlobalHotkeys"/> over the static <see cref="GlobalHotkeys"/> API.
/// </summary>
public sealed class DesktopGlobalHotkeys : IGlobalHotkeys
{
    public event Action<string>? Activated
    {
        add => GlobalHotkeys.Activated += value;
        remove => GlobalHotkeys.Activated -= value;
    }

    public Task<HermesHotkeyResult> RegisterAsync(string id, string accelerator, string? description = null) =>
        GlobalHotkeys.RegisterAsync(id, accelerator, description);

    public Task UnregisterAsync(string id) => GlobalHotkeys.UnregisterAsync(id);
}
