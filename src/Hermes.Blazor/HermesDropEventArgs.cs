// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text.Json.Serialization;
using Hermes.Contracts.DataTransfer;
using Hermes.DataTransfer;

namespace Hermes.Blazor;

/// <summary>
/// Arguments for <c>@onhermesdrop</c>: what the page saw, plus the dropped data with real paths.
/// </summary>
public sealed class HermesDropEventArgs : EventArgs, IJsonOnDeserialized
{
    /// <summary>Opaque ID of the drop.</summary>
    public string DropId { get; set; } = "";

    /// <summary>Horizontal drop point in CSS pixels, relative to the page viewport.</summary>
    public double X { get; set; }

    /// <summary>Vertical drop point in CSS pixels, relative to the page viewport.</summary>
    public double Y { get; set; }

    /// <summary>Name, size and type of each dropped file, as the page saw them.</summary>
    public IReadOnlyList<HermesDropFile> Files { get; set; } = [];

    /// <summary>The DOM's dataTransfer types.</summary>
    public IReadOnlyList<string> Types { get; set; } = [];

    /// <summary>Everything dropped, with real paths; empty when the ID does not resolve.</summary>
    [JsonIgnore]
    public HermesDataPackage Data { get; private set; } = new();

    void IJsonOnDeserialized.OnDeserialized() =>
        Data = DropRegistry.Shared.TryGet(DropId, owner: null, out var drop) ? drop.Data : new HermesDataPackage();
}

/// <summary>
/// A dropped file as the page saw it. Its path is in <see cref="HermesDropEventArgs.Data"/>.
/// </summary>
public sealed record HermesDropFile(string Name, long Size, string Type);
