// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace Hermes.DataTransfer;

/// <summary>
/// The Windows <c>DROPFILES</c> structure behind <c>CF_HDROP</c>: a 20-byte header, then NUL-separated paths.
/// </summary>
internal static class DropFilesFormat
{
    private const int HeaderSize = 20;
    private const int WideFlagOffset = 16;

    public static byte[] Build(IReadOnlyList<string> paths)
    {
        var charCount = paths.Sum(p => p.Length + 1) + 1;
        var buffer = new byte[HeaderSize + charCount * sizeof(char)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, HeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(WideFlagOffset), 1);

        var offset = HeaderSize;
        foreach (var path in paths)
            offset += Encoding.Unicode.GetBytes(path, buffer.AsSpan(offset)) + sizeof(char);

        return buffer;
    }

    public static IReadOnlyList<string> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
            return [];

        var filesOffset = BinaryPrimitives.ReadInt32LittleEndian(data);
        var wide = BinaryPrimitives.ReadInt32LittleEndian(data[WideFlagOffset..]) != 0;
        if (filesOffset < HeaderSize || filesOffset > data.Length)
            return [];

        var paths = new List<string>();
        var rest = data[filesOffset..];
        if (wide)
        {
            var chars = MemoryMarshal.Cast<byte, char>(rest[..(rest.Length & ~1)]);
            for (var end = chars.IndexOf('\0'); end > 0; end = chars.IndexOf('\0'))
            {
                paths.Add(new string(chars[..end]));
                chars = chars[(end + 1)..];
            }
        }
        else
        {
            // Legacy ANSI lists; Latin1 matches code page 1252 for the characters that appear in paths.
            for (var end = rest.IndexOf((byte)0); end > 0; end = rest.IndexOf((byte)0))
            {
                paths.Add(Encoding.Latin1.GetString(rest[..end]));
                rest = rest[(end + 1)..];
            }
        }

        return paths;
    }
}
