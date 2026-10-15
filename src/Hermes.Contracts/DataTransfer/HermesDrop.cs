// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.DataTransfer;

/// <summary>
/// Data another app dropped on a window.
/// </summary>
public sealed class HermesDrop
{
    /// <summary>Opaque ID the page received in its hermesdrop event.</summary>
    public required string Id { get; init; }

    /// <summary>Horizontal drop point in CSS pixels, relative to the page viewport.</summary>
    public required double X { get; init; }

    /// <summary>Vertical drop point in CSS pixels, relative to the page viewport.</summary>
    public required double Y { get; init; }

    /// <summary>Everything dropped, with real file paths.</summary>
    public required HermesDataPackage Data { get; init; }
}
