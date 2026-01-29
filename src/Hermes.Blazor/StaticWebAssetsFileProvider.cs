using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Hermes.Blazor;

/// <summary>
/// File provider that resolves static web assets from the runtime manifest.
/// This enables serving files from NuGet packages and Razor Class Libraries.
/// </summary>
internal sealed class StaticWebAssetsFileProvider : IFileProvider
{
    private readonly string[] _contentRoots;
    private readonly Dictionary<string, AssetInfo> _assets;
    private readonly IFileProvider _fallbackProvider;
    private readonly string _baseDirectory;

    private StaticWebAssetsFileProvider(
        string[] contentRoots,
        Dictionary<string, AssetInfo> assets,
        IFileProvider fallbackProvider)
    {
        _contentRoots = contentRoots;
        _assets = assets;
        _fallbackProvider = fallbackProvider;
        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// Creates a file provider that reads from the static web assets manifest.
    /// Falls back to the provided file provider if no manifest is found.
    /// </summary>
    public static IFileProvider Create(string appName, IFileProvider fallbackProvider)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var manifestPath = Path.Combine(baseDir, $"{appName}.staticwebassets.runtime.json");

        if (!File.Exists(manifestPath))
            return fallbackProvider;

        try
        {
            var manifest = ParseManifest(manifestPath);
            return new StaticWebAssetsFileProvider(
                manifest.ContentRoots,
                manifest.Assets,
                fallbackProvider);
        }
        catch
        {
            return fallbackProvider;
        }
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        var normalizedPath = subpath.TrimStart('/');
        var queryIndex = normalizedPath.IndexOf('?');
        if (queryIndex >= 0)
        {
            normalizedPath = normalizedPath[..queryIndex];
        }

        if (_assets.TryGetValue(normalizedPath, out var asset))
        {
            // Try 1: Absolute path from manifest (works in dev)
            var contentRoot = _contentRoots[asset.ContentRootIndex];
            var fullPath = Path.Combine(contentRoot, asset.SubPath);

            if (File.Exists(fullPath))
            {
                return new PhysicalFileInfo(new FileInfo(fullPath));
            }

            // Try 2: Relative path under wwwroot (works in published builds)
            // The asset path in manifest looks like "_content/PackageName/file.js"
            var relativePath = Path.Combine(_baseDirectory, "wwwroot", normalizedPath);
            if (File.Exists(relativePath))
            {
                return new PhysicalFileInfo(new FileInfo(relativePath));
            }
        }

        return _fallbackProvider.GetFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) =>
        _fallbackProvider.GetDirectoryContents(subpath);

    public IChangeToken Watch(string filter) =>
        NullChangeToken.Singleton;

    private static (string[] ContentRoots, Dictionary<string, AssetInfo> Assets) ParseManifest(string manifestPath)
    {
        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var contentRootsArray = root.GetProperty("ContentRoots");
        var contentRoots = new string[contentRootsArray.GetArrayLength()];
        for (int i = 0; i < contentRoots.Length; i++)
        {
            contentRoots[i] = contentRootsArray[i].GetString()!;
        }

        var assets = new Dictionary<string, AssetInfo>(StringComparer.OrdinalIgnoreCase);
        var rootNode = root.GetProperty("Root");
        ParseNode(rootNode, "", assets);

        return (contentRoots, assets);
    }

    private static void ParseNode(JsonElement node, string currentPath, Dictionary<string, AssetInfo> assets)
    {
        if (node.TryGetProperty("Asset", out var assetProp) && assetProp.ValueKind != JsonValueKind.Null)
        {
            var contentRootIndex = assetProp.GetProperty("ContentRootIndex").GetInt32();
            var subPath = assetProp.GetProperty("SubPath").GetString()!;
            assets[currentPath] = new AssetInfo(contentRootIndex, subPath);
        }

        if (node.TryGetProperty("Children", out var children) && children.ValueKind != JsonValueKind.Null)
        {
            foreach (var child in children.EnumerateObject())
            {
                var childPath = string.IsNullOrEmpty(currentPath)
                    ? child.Name
                    : $"{currentPath}/{child.Name}";
                ParseNode(child.Value, childPath, assets);
            }
        }
    }

    private readonly record struct AssetInfo(int ContentRootIndex, string SubPath);

    /// <summary>
    /// Simple file info wrapper for physical files.
    /// </summary>
    private sealed class PhysicalFileInfo : IFileInfo
    {
        private readonly FileInfo _fileInfo;

        public PhysicalFileInfo(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
        }

        public bool Exists => _fileInfo.Exists;
        public long Length => _fileInfo.Length;
        public string? PhysicalPath => _fileInfo.FullName;
        public string Name => _fileInfo.Name;
        public DateTimeOffset LastModified => _fileInfo.LastWriteTimeUtc;
        public bool IsDirectory => false;

        public Stream CreateReadStream() => _fileInfo.OpenRead();
    }
}
