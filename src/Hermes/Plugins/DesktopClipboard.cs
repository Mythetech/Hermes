// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Contracts.Plugins;

namespace Hermes.Plugins;

/// <summary>
/// Desktop implementation of <see cref="IClipboard"/> that adapts the synchronous
/// <see cref="Hermes.Clipboard"/> static API to the async interface used by DI consumers.
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
}
