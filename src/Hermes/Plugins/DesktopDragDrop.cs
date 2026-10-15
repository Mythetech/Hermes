// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.Contracts.Plugins;

namespace Hermes.Plugins;

/// <summary>
/// Desktop implementation of <see cref="IDragDrop"/> over a <see cref="HermesWindow"/>.
/// </summary>
public sealed class DesktopDragDrop : IDragDrop
{
    private readonly HermesWindow _window;

    public DesktopDragDrop(HermesWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    public event Action<HermesDrop>? Dropped
    {
        add => _window.Dropped += value;
        remove => _window.Dropped -= value;
    }

    public Task<HermesDragResult> StartDragAsync(HermesDataPackage package) => _window.StartDragAsync(package);
}
