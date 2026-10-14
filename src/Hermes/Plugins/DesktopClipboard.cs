// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;
using Hermes.Contracts.Plugins;

namespace Hermes.Plugins;

/// <summary>
/// Desktop implementation of <see cref="IClipboard"/> over the static <see cref="Hermes.Clipboard"/> API.
/// </summary>
public sealed class DesktopClipboard : IClipboard
{
    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        Clipboard.SetText(text);
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken ct = default)
        => Task.FromResult(Clipboard.GetText());

    public Task<HermesDataFormats> GetFormatsAsync(CancellationToken ct = default)
        => Task.FromResult(Clipboard.GetFormats());

    public Task<HermesDataPackage> ReadAsync(HermesDataFormats formats, IReadOnlyList<string>? customTypes = null, CancellationToken ct = default)
        => Task.FromResult(Clipboard.Read(formats, customTypes));

    public Task WriteAsync(HermesDataPackage package, CancellationToken ct = default)
    {
        Clipboard.Write(package);
        return Task.CompletedTask;
    }

    public event Action? Changed
    {
        add => Clipboard.Changed += value;
        remove => Clipboard.Changed -= value;
    }
}
