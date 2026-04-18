// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Foundation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.Primitives;

namespace Hermes.Mobile.WebView;

/// <summary>
/// Serves static assets from the app bundle's wwwroot folder.
/// </summary>
/// <remarks>
/// The .app bundle is a directory on disk; NSBundle.MainBundle.ResourcePath points at
/// its Resources root. Blazor's published wwwroot ends up under {bundle}/wwwroot/ when
/// BundleResource is configured to preserve that layout.
/// </remarks>
internal sealed class IOSAssetFileProvider : IFileProvider
{
    private readonly string _bundleRootDir;

    public IOSAssetFileProvider(string contentRootDir)
    {
        var resourcePath = NSBundle.MainBundle.ResourcePath
            ?? throw new InvalidOperationException("NSBundle.MainBundle.ResourcePath is null");
        _bundleRootDir = Path.Combine(resourcePath, contentRootDir);
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
            return new NotFoundFileInfo(subpath);

        var normalized = subpath.TrimStart('/');
        var candidate = Path.GetFullPath(Path.Combine(_bundleRootDir, normalized));

        // Path-traversal guard: resolved path must stay within the bundle root.
        if (!candidate.StartsWith(_bundleRootDir, StringComparison.Ordinal))
            return new NotFoundFileInfo(subpath);

        return File.Exists(candidate)
            ? new PhysicalFileInfo(new FileInfo(candidate))
            : new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
        => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
}
