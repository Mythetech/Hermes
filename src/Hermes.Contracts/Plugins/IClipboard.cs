// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Contracts.Plugins;

/// <summary>
/// Provides asynchronous, DI-friendly access to the system clipboard.
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// Writes text to the system clipboard.
    /// </summary>
    /// <param name="text">The text to copy. Must not be null or whitespace on platforms that reject empty values.</param>
    /// <param name="ct">Optional cancellation token. Honored on a best-effort basis by platform implementations.</param>
    Task SetTextAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Reads text from the system clipboard.
    /// Returns <c>null</c> when the clipboard is empty or does not contain text.
    /// </summary>
    /// <param name="ct">Optional cancellation token. Honored on a best-effort basis by platform implementations.</param>
    Task<string?> GetTextAsync(CancellationToken ct = default);
}
