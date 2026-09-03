// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Hermes.Tests.Web;

/// <summary>
/// In-memory IFileProvider so startup tests can control exactly which static
/// assets exist without touching the disk.
/// </summary>
internal sealed class TestFileProvider : IFileProvider
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public TestFileProvider Add(string path, string content)
    {
        _files[path.TrimStart('/')] = Encoding.UTF8.GetBytes(content);
        return this;
    }

    public IFileInfo GetFileInfo(string subpath) =>
        _files.TryGetValue(subpath.TrimStart('/'), out var bytes)
            ? new MemoryFileInfo(Path.GetFileName(subpath), bytes)
            : new NotFoundFileInfo(subpath);

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    private sealed class MemoryFileInfo(string name, byte[] bytes) : IFileInfo
    {
        public bool Exists => true;
        public long Length => bytes.Length;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(bytes, writable: false);
    }
}
