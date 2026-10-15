// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;

namespace Hermes.Abstractions;

/// <summary>
/// What a window's drop script reported.
/// </summary>
public enum HermesDropReportKind
{
    /// <summary>An outside drop landed in the page.</summary>
    Drop,

    /// <summary>A new document started, so the previous document's drops are dead.</summary>
    Reset,
}

/// <summary>
/// A report from a window's drop script, with the data the backend captured for it.
/// </summary>
public sealed class HermesDropReport
{
    /// <summary>What happened.</summary>
    public required HermesDropReportKind Kind { get; init; }

    /// <summary>Number the script uses to match the reply to the drop.</summary>
    public long Sequence { get; init; }

    /// <summary>Horizontal drop point in CSS pixels, relative to the page viewport.</summary>
    public double X { get; init; }

    /// <summary>Vertical drop point in CSS pixels, relative to the page viewport.</summary>
    public double Y { get; init; }

    /// <summary>The dropped data, or null when the backend has nothing for this drop.</summary>
    public HermesDataPackage? Data { get; init; }
}
