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

    /// <summary>
    /// Starts raising <see cref="Changed"/>. This and <see cref="StopMonitoring"/> must not block waiting on another thread
    /// (post the work instead), because Clipboard calls them under its subscription lock.
    /// </summary>
    void StartMonitoring();

    /// <summary>Stops raising <see cref="Changed"/>; see <see cref="StartMonitoring"/> for the no-blocking rule.</summary>
    void StopMonitoring();

    /// <summary>Raised on the backend's thread while monitoring; Clipboard posts to each subscriber's context.</summary>
    event Action? Changed;
}
