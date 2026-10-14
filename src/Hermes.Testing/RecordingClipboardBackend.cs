// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.Testing;

/// <summary>
/// An in-memory clipboard for Hermes's own tests, installed through <c>Clipboard.SetBackendForTesting</c>.
/// </summary>
public sealed class RecordingClipboardBackend : IClipboardBackend
{
    private readonly List<string> _operations = new();

    public event Action? Changed;

    public HermesDataPackage Content { get; set; } = new();

    public IReadOnlyList<string> Operations => _operations;

    public bool IsMonitoring { get; private set; }

    public bool IsSupported { get; set; } = true;

    public string? UnsupportedReason { get; set; }

    public int DisposeCount { get; private set; }

    public HermesDataFormats GetFormats()
    {
        _operations.Add("GetFormats");
        return Content.Formats;
    }

    public bool HasCustom(string mimeType)
    {
        _operations.Add($"HasCustom:{mimeType}");
        return Content.Custom.ContainsKey(mimeType);
    }

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes)
    {
        _operations.Add($"Read:{formats}");

        var custom = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in customTypes)
        {
            if (Content.Custom.TryGetValue(type, out var data))
                custom[type] = data;
        }

        return new HermesDataPackage
        {
            Text = formats.HasFlag(HermesDataFormats.Text) ? Content.Text : null,
            Html = formats.HasFlag(HermesDataFormats.Html) ? Content.Html : null,
            Png = formats.HasFlag(HermesDataFormats.Png) ? Content.Png : null,
            Files = formats.HasFlag(HermesDataFormats.Files) ? Content.Files : null,
            Custom = custom,
        };
    }

    public void Write(HermesDataPackage package)
    {
        _operations.Add("Write");
        Content = package;
    }

    public void Clear()
    {
        _operations.Add("Clear");
        Content = new HermesDataPackage();
    }

    public void StartMonitoring()
    {
        _operations.Add("StartMonitoring");
        IsMonitoring = true;
    }

    public void StopMonitoring()
    {
        _operations.Add("StopMonitoring");
        IsMonitoring = false;
    }

    public void RaiseChanged() => Changed?.Invoke();

    public void Dispose() => DisposeCount++;
}
