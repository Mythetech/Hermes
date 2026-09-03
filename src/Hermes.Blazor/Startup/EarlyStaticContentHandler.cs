// Copyright (c) Mythetech. Licensed under the MIT License.
using Hermes.Diagnostics;
using Microsoft.Extensions.FileProviders;

namespace Hermes.Blazor.Startup;

/// <summary>
/// Serves scheme requests that arrive before the WebViewManager exists, so the
/// WebView can load the host page and boot its script while managed composition
/// is still running. Reads from the same file provider the manager will use.
/// Never waits: anything it cannot serve is a 404.
/// </summary>
internal sealed class EarlyStaticContentHandler
{
    private readonly Lazy<IFileProvider> _fileProvider;
    private readonly InlinedHostPage _hostPage;

    public EarlyStaticContentHandler(Lazy<IFileProvider> fileProvider, InlinedHostPage hostPage)
    {
        _fileProvider = fileProvider;
        _hostPage = hostPage;
    }

    public (Stream? Content, string? ContentType) Handle(string url)
    {
        try
        {
            var path = Uri.UnescapeDataString(new Uri(url).AbsolutePath);
            var hasExtension = path.LastIndexOf('.') > path.LastIndexOf('/');

            // Extensionless paths are client-side routes and get the host page,
            // which is the same fallback WebViewManager applies.
            if (!hasExtension || _hostPage.Matches(path))
            {
                return _hostPage.TryGetBytes(out var bytes)
                    ? (new MemoryStream(bytes, writable: false), "text/html")
                    : (null, null);
            }

            var file = _fileProvider.Value.GetFileInfo(path.TrimStart('/'));
            if (!file.Exists || file.IsDirectory)
                return (null, null);

            // A null content type lets the backend apply its extension mapping,
            // the same fallback it already uses for the manager's responses.
            return (file.CreateReadStream(), null);
        }
        catch (Exception ex)
        {
            HermesLogger.Warning($"Early static content handler could not serve '{url}': {ex.Message}");
            return (null, null);
        }
    }
}
