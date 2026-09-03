// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor.Startup;

/// <summary>
/// The host page with blazor.webview.js inlined, built once from the static file
/// provider. The early startup handler and the WebViewManager both serve from it
/// so the WebView sees identical bytes no matter which of them answers. The
/// provider itself is resolved on first use, so constructing this costs nothing
/// on the UI thread.
/// </summary>
internal sealed class InlinedHostPage
{
    private readonly Lazy<IFileProvider> _fileProvider;
    private readonly object _lock = new();
    private byte[]? _bytes;
    private bool _resolved;

    public InlinedHostPage(Lazy<IFileProvider> fileProvider, string hostPagePath)
    {
        _fileProvider = fileProvider;
        HostPagePath = hostPagePath;
    }

    public string HostPagePath { get; }

    /// <summary>
    /// Returns the inlined page bytes, or false when the host page does not exist
    /// in the file provider. The first call reads and inlines; later calls return
    /// the cached array. Callers must not modify the returned array, since every
    /// response shares it.
    /// </summary>
    public bool TryGetBytes(out byte[] bytes)
    {
        lock (_lock)
        {
            if (!_resolved)
            {
                _bytes = Build();
                _resolved = true;
            }

            bytes = _bytes ?? [];
            return _bytes is not null;
        }
    }

    /// <summary>
    /// True for the app root and for the host page itself, the two paths whose
    /// correct response is the inlined page.
    /// </summary>
    public bool Matches(string absolutePath) =>
        absolutePath == "/" ||
        absolutePath.EndsWith("/" + HostPagePath, StringComparison.OrdinalIgnoreCase);

    private byte[]? Build()
    {
        var html = ReadText(HostPagePath);
        if (html is null)
            return null;

        var inlined = HostPageInliner.Inline(html, ReadText);
        return Encoding.UTF8.GetBytes(inlined);
    }

    private string? ReadText(string subpath)
    {
        var file = _fileProvider.Value.GetFileInfo(subpath);
        if (!file.Exists || file.IsDirectory)
            return null;

        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
