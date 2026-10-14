// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;

namespace Hermes.Abstractions;

/// <summary>
/// Platform-specific clipboard backend. <see cref="Hermes.Clipboard"/> validates every argument before calling it.
/// </summary>
internal interface IClipboardBackend : IDisposable
{
    bool IsSupported { get; }

    string? UnsupportedReason { get; }

    HermesDataFormats GetFormats();

    bool HasCustom(string mimeType);

    HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes);

    void Write(HermesDataPackage package);

    void Clear();

    void StartMonitoring();

    void StopMonitoring();

    /// <summary>Raised on the backend's thread while monitoring; Clipboard posts to each subscriber's context.</summary>
    event Action? Changed;
}
