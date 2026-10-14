// Copyright (c) Mythetech. Licensed under the MIT License.
namespace Hermes.Contracts.DataTransfer;

/// <summary>
/// An immutable snapshot of data in one or more formats, shared by the clipboard and drag and drop.
/// </summary>
public sealed class HermesDataPackage
{
    private static readonly IReadOnlyDictionary<string, ReadOnlyMemory<byte>> s_noCustom =
        new Dictionary<string, ReadOnlyMemory<byte>>(0);

    private readonly IReadOnlyDictionary<string, ReadOnlyMemory<byte>> _custom = s_noCustom;

    /// <summary>Plain text.</summary>
    public string? Text { get; init; }

    /// <summary>An HTML fragment, without any platform header.</summary>
    public string? Html { get; init; }

    /// <summary>A PNG image. Images in other formats are converted to PNG when read.</summary>
    public ReadOnlyMemory<byte>? Png { get; init; }

    /// <summary>Absolute file paths.</summary>
    public IReadOnlyList<string>? Files { get; init; }

    /// <summary>App-defined formats keyed by MIME type. Never null.</summary>
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Custom
    {
        get => _custom;
        init => _custom = value ?? s_noCustom;
    }

    /// <summary>The standard formats this package carries.</summary>
    public HermesDataFormats Formats =>
        (Text is null ? HermesDataFormats.None : HermesDataFormats.Text)
        | (Html is null ? HermesDataFormats.None : HermesDataFormats.Html)
        | (Png is null ? HermesDataFormats.None : HermesDataFormats.Png)
        | (Files is null ? HermesDataFormats.None : HermesDataFormats.Files);

    /// <summary>True when the package carries no standard format and no custom format.</summary>
    public bool IsEmpty => Formats == HermesDataFormats.None && Custom.Count == 0;
}
