// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
namespace Hermes.Web.Hosting;

internal sealed class StaticFileHost
{
    private readonly string _rootDirectory;
    private readonly bool _spaFallback;

    public StaticFileHost(string rootDirectory, bool spaFallback)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _spaFallback = spaFallback;
    }

    public (Stream? Content, string? ContentType) HandleRequest(string url)
    {
        var path = ExtractPath(url);
        if (string.IsNullOrEmpty(path) || path == "/")
            path = "/index.html";

        var filePath = ResolveFilePath(path);

        if (filePath is not null && File.Exists(filePath))
            return (File.OpenRead(filePath), MimeTypes.GetContentType(filePath));

        if (_spaFallback && ShouldFallback(path))
        {
            var indexPath = Path.Combine(_rootDirectory, "index.html");
            if (File.Exists(indexPath))
                return (File.OpenRead(indexPath), "text/html");
        }

        return (null, null);
    }

    private string? ResolveFilePath(string urlPath)
    {
        var relativePath = urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));

        if (!fullPath.StartsWith(_rootDirectory, StringComparison.Ordinal))
            return null;

        return fullPath;
    }

    private static bool ShouldFallback(string path)
    {
        var ext = Path.GetExtension(path);
        return string.IsNullOrEmpty(ext);
    }

    private static string ExtractPath(string url)
    {
        var queryIndex = url.IndexOf('?');
        if (queryIndex >= 0)
            url = url[..queryIndex];

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return uri.AbsolutePath;

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0)
        {
            var pathStart = url.IndexOf('/', schemeEnd + 3);
            return pathStart >= 0 ? url[pathStart..] : "/";
        }

        return url;
    }
}
