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

    /// <summary>Returns only requested formats that are present; Custom holds the requested types found, compared case-insensitively.</summary>
    HermesDataPackage Read(HermesDataFormats formats, IReadOnlyList<string> customTypes);

    void Write(HermesDataPackage package);

    void Clear();

    /// <summary>
    /// Start/StopMonitoring run under Clipboard's subscription lock, possibly from a Changed handler on the backend's own thread.
    /// They must never block on the UI thread or on a thread that can be running a Changed handler.
    /// </summary>
    void StartMonitoring();

    /// <summary>Stops raising <see cref="Changed"/>; see <see cref="StartMonitoring"/> for the threading rule.</summary>
    void StopMonitoring();

    /// <summary>Raised on the backend's thread while monitoring; Clipboard posts to each subscriber's context.</summary>
    event Action? Changed;
}
