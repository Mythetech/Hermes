// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Abstractions;
using Hermes.Contracts.DataTransfer;

namespace Hermes.DataTransfer;

internal sealed class UnsupportedClipboardBackend : IClipboardBackend
{
    public UnsupportedClipboardBackend(string reason) => UnsupportedReason = reason;

    public bool IsSupported => false;

    public string? UnsupportedReason { get; }

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public HermesDataFormats GetFormats() => throw NotSupported();

    public bool HasCustom(string mimeType) => throw NotSupported();

    public HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes) => throw NotSupported();

    public void Write(HermesDataPackage package) => throw NotSupported();

    public void Clear() => throw NotSupported();

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void Dispose()
    {
    }

    private PlatformNotSupportedException NotSupported() => new(UnsupportedReason);
}
