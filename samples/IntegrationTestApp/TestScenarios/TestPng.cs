// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace IntegrationTestApp.TestScenarios;

/// <summary>Builds noise PNGs so clipboard scenarios can round-trip images of a chosen size.</summary>
internal static class TestPng
{
    private static readonly uint[] s_crcTable = BuildCrcTable();

    public static byte[] Create(int width, int height, int seed)
    {
        var random = new Random(seed);
        var rowLength = 1 + width * 4;
        var raw = new byte[height * rowLength];
        for (var y = 0; y < height; y++)
            random.NextBytes(raw.AsSpan(y * rowLength + 1, width * 4));

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);

        using (var compressed = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                zlib.Write(raw);
            WriteChunk(png, "IDAT", compressed.ToArray());
        }

        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        var typeBytes = Encoding.ASCII.GetBytes(type);

        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        stream.Write(word);
        stream.Write(typeBytes);
        stream.Write(data);

        var crc = Update(Update(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(word, crc);
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
