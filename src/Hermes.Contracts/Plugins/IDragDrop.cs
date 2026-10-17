// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;

namespace Hermes.Contracts.Plugins;

/// <summary>
/// Native drag and drop for the app's window.
/// </summary>
public interface IDragDrop
{
    /// <summary>Raised on the UI thread when another app's data is dropped on the window.</summary>
    event Action<HermesDrop>? Dropped;

    /// <summary>
    /// Starts a native drag of the package, offering Copy. Call while the primary mouse button is held,
    /// typically from a cancelled dragstart. Completes when the drag ends.
    /// </summary>
    Task<HermesDragResult> StartDragAsync(HermesDataPackage package);
}
