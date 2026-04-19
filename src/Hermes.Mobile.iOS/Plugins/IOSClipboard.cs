// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Hermes.Contracts.Plugins;
using UIKit;

namespace Hermes.Mobile.iOS.Plugins;

/// <summary>
/// iOS implementation of <see cref="IClipboard"/> backed by <see cref="UIPasteboard.General"/>.
/// </summary>
public sealed class IOSClipboard : IClipboard
{
    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        UIPasteboard.General.String = text;
        return Task.CompletedTask;
    }

    public Task<string?> GetTextAsync(CancellationToken ct = default)
        => Task.FromResult<string?>(UIPasteboard.General.String);
}
