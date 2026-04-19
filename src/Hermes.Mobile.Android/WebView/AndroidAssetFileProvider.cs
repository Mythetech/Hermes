// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
using Android.Content.Res;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Hermes.Mobile.Android.WebView;

internal sealed class AndroidAssetFileProvider : IFileProvider
{
    private readonly AssetManager _assets;
    private readonly string _root;

    public AndroidAssetFileProvider(AssetManager assets, string contentRoot)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _root = contentRoot.TrimEnd('/');
    }

    public IFileInfo GetFileInfo(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
            return new NotFoundFileInfo(subpath);

        var normalized = subpath.TrimStart('/');

        if (normalized.Contains(".."))
            return new NotFoundFileInfo(subpath);

        var assetPath = string.IsNullOrEmpty(_root) ? normalized : $"{_root}/{normalized}";

        try
        {
            var stream = _assets.Open(assetPath);
            return new AndroidAssetFileInfo(stream, Path.GetFileName(normalized));
        }
        catch (Java.IO.FileNotFoundException)
        {
            return new NotFoundFileInfo(subpath);
        }
    }

    public IDirectoryContents GetDirectoryContents(string subpath)
        => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private sealed class AndroidAssetFileInfo : IFileInfo
    {
        private readonly System.IO.Stream _stream;

        public AndroidAssetFileInfo(System.IO.Stream stream, string name)
        {
            _stream = stream;
            Name = name;
        }

        public bool Exists => true;
        public long Length => -1;
        public string? PhysicalPath => null;
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.MinValue;
        public bool IsDirectory => false;

        public System.IO.Stream CreateReadStream() => _stream;
    }
}
