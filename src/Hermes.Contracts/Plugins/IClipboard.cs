// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Contracts.DataTransfer;

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

    /// <summary>
    /// Reports which standard formats the clipboard holds. The default reports text only.
    /// </summary>
    async Task<HermesDataFormats> GetFormatsAsync(CancellationToken ct = default) =>
        await GetTextAsync(ct) is null ? HermesDataFormats.None : HermesDataFormats.Text;

    /// <summary>
    /// Reads the requested formats and listed custom types. The default fills only <see cref="HermesDataPackage.Text"/>.
    /// </summary>
    async Task<HermesDataPackage> ReadAsync(HermesDataFormats formats, IReadOnlyList<string>? customTypes = null, CancellationToken ct = default) =>
        formats.HasFlag(HermesDataFormats.Text)
            ? new HermesDataPackage { Text = await GetTextAsync(ct) }
            : new HermesDataPackage();

    /// <summary>
    /// Replaces the clipboard with every format in the package. The default accepts text-only packages.
    /// </summary>
    Task WriteAsync(HermesDataPackage package, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Formats != HermesDataFormats.Text || package.Custom.Count > 0)
            throw new NotSupportedException("This clipboard implementation only supports text.");

        return SetTextAsync(package.Text!, ct);
    }

    /// <summary>
    /// Raised when the clipboard changes. The default never raises it.
    /// </summary>
    event Action? Changed
    {
        add { }
        remove { }
    }
}
