// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.IO.Compression;

namespace Hermes.DataTransfer;

/// <summary>
/// Writes 8-bit RGBA images as PNG, for clipboard images that arrive in another format.
/// </summary>
internal static class PngEncoder
{
    private static readonly uint[] s_crcTable = BuildCrcTable();

    public static byte[] EncodeRgba(int width, int height, ReadOnlySpan<byte> rgba)
    {
        var rowLength = width * 4;
        var raw = new byte[height * (rowLength + 1)];
        for (var y = 0; y < height; y++)
            rgba.Slice(y * rowLength, rowLength).CopyTo(raw.AsSpan(y * (rowLength + 1) + 1));

        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR"u8, header);

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw);
            WriteChunk(output, "IDAT"u8, compressed.ToArray());
        }

        WriteChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    public static uint Crc32(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data) =>
        Update(Update(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu;

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        stream.Write(word);
        stream.Write(type);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(type, data));
        stream.Write(word);
    }

    private static uint Update(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
            crc = s_crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
