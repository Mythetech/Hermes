// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.IO.Compression;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class PngEncoderTests
{
    [Fact]
    public void Crc32_OfEmptyIend_IsTheWellKnownValue() =>
        Assert.Equal(0xAE426082u, PngEncoder.Crc32("IEND"u8, []));

    [Fact]
    public void EncodeRgba_WritesSignatureHeaderAndPixels()
    {
        byte[] rgba = [1, 2, 3, 4, 5, 6, 7, 8];

        var png = PngEncoder.EncodeRgba(2, 1, rgba);

        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        Assert.Equal("IHDR"u8.ToArray(), png[12..16]);
        Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);

        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
        Assert.Equal("IDAT"u8.ToArray(), png[37..41]);
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress))
            zlib.CopyTo(inflated);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, inflated.ToArray());

        Assert.Equal("IEND"u8.ToArray(), png[^8..^4]);
    }
}
