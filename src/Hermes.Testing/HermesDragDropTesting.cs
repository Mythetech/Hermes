// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;

namespace Hermes.Testing;

/// <summary>
/// Drives a real window's drop path without an OS drag.
/// </summary>
public static class HermesDragDropTesting
{
    /// <summary>
    /// Queues the data as if the platform captured it and asks the page's drop script to report a drop at the point
    /// (CSS pixels). Completes with the drop once .NET has registered it and raised Dropped.
    /// </summary>
    public static Task<HermesDrop> SimulateDropAsync(HermesWindow window, HermesDataPackage data, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(data);
        return window.SimulateDropForTesting(data, x, y);
    }
}
