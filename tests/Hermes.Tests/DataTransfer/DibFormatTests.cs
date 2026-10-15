// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;
using System.IO.Compression;
using Hermes.DataTransfer;
using Xunit;

namespace Hermes.Tests.DataTransfer;

public sealed class DibFormatTests
{
    // Decodes the single IDAT of a PngEncoder image back to RGBA rows, dropping the filter bytes.
    private static byte[] PngPixels(byte[] png, int width, int height)
    {
        var idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress))
            zlib.CopyTo(inflated);

        var raw = inflated.ToArray();
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            raw.AsSpan(y * (width * 4 + 1) + 1, width * 4).CopyTo(pixels.AsSpan(y * width * 4));
        return pixels;
    }

    // Masks go at offset 40: after a 40-byte header, or inside a V4/V5 header.
    private static byte[] Dib(int width, int height, short bitCount, int compression, byte[] pixelRows, int headerSize = 40, byte[]? masks = null, int colorsUsed = 0)
    {
        var trailingMasks = headerSize == 40 ? masks?.Length ?? 0 : 0;
        var dib = new byte[headerSize + trailingMasks + pixelRows.Length];
        BinaryPrimitives.WriteInt32LittleEndian(dib, headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), height);
        BinaryPrimitives.WriteInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteInt16LittleEndian(dib.AsSpan(14), bitCount);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(16), compression);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(32), colorsUsed);
        masks?.CopyTo(dib, 40);
        pixelRows.CopyTo(dib, headerSize + trailingMasks);
        return dib;
    }

    private static byte[] Masks(params uint[] values)
    {
        var masks = new byte[values.Length * 4];
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(masks.AsSpan(i * 4), values[i]);
        return masks;
    }

    [Fact]
    public void ToPng_BottomUp32Bit_ZeroAlpha_IsOpaqueAndUpright()
    {
        // Bottom-up: the first row in memory is the bottom row. BGRA with alpha 0, as PrintScreen leaves it.
        byte[] rows = [10, 20, 30, 0, 40, 50, 60, 0];
        var png = DibFormat.ToPng(Dib(1, 2, 32, 0, rows))!;

        Assert.Equal(new byte[] { 60, 50, 40, 255, 30, 20, 10, 255 }, PngPixels(png, 1, 2));
    }

    [Fact]
    public void ToPng_TopDown_KeepsRowOrder()
    {
        byte[] rows = [10, 20, 30, 0, 40, 50, 60, 0];
        var png = DibFormat.ToPng(Dib(1, -2, 32, 0, rows))!;

        Assert.Equal(new byte[] { 30, 20, 10, 255, 60, 50, 40, 255 }, PngPixels(png, 1, 2));
    }

    [Fact]
    public void ToPng_32BitWithAlpha_KeepsAlpha()
    {
        byte[] rows = [10, 20, 30, 128];
        var png = DibFormat.ToPng(Dib(1, 1, 32, 0, rows))!;

        Assert.Equal(new byte[] { 30, 20, 10, 128 }, PngPixels(png, 1, 1));
    }

    [Fact]
    public void ToPng_24Bit_PadsRowsToFourBytes()
    {
        // Two 24-bit pixels are 6 bytes, padded to 8 per row.
        byte[] rows = [1, 2, 3, 4, 5, 6, 0, 0];
        var png = DibFormat.ToPng(Dib(2, 1, 24, 0, rows))!;

        Assert.Equal(new byte[] { 3, 2, 1, 255, 6, 5, 4, 255 }, PngPixels(png, 2, 1));
    }

    [Fact]
    public void ToPng_Bitfields_WithStandardMasks_IsRead()
    {
        var masks = Masks(0x00FF0000, 0x0000FF00, 0x000000FF);
        var png = DibFormat.ToPng(Dib(1, 1, 32, 3, [10, 20, 30, 255], masks: masks))!;

        Assert.Equal(new byte[] { 30, 20, 10, 255 }, PngPixels(png, 1, 1));
    }

    [Fact]
    public void ToPng_V5HeaderWithBitfields_ReadsTheMasksInsideTheHeader()
    {
        var masks = Masks(0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000);
        var png = DibFormat.ToPng(Dib(1, 1, 32, 3, [10, 20, 30, 128], headerSize: 124, masks: masks))!;

        Assert.Equal(new byte[] { 30, 20, 10, 128 }, PngPixels(png, 1, 1));
    }

    [Fact]
    public void ToPng_V5HeaderWithRgb_IsRead()
    {
        var png = DibFormat.ToPng(Dib(1, 1, 32, 0, [10, 20, 30, 128], headerSize: 124))!;

        Assert.Equal(new byte[] { 30, 20, 10, 128 }, PngPixels(png, 1, 1));
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(32, 1)]
    public void ToPng_UnsupportedBitCountOrCompression_ReturnsNull(short bitCount, int compression) =>
        Assert.Null(DibFormat.ToPng(Dib(1, 1, bitCount, compression, new byte[4])));

    [Fact]
    public void ToPng_Truncated_ReturnsNull() =>
        Assert.Null(DibFormat.ToPng(Dib(4, 4, 32, 0, new byte[4])));

    [Fact]
    public void ToPng_WidthWhoseStrideWrapsInt_ReturnsNull() =>
        Assert.Null(DibFormat.ToPng(Dib(1 << 27, 1, 32, 0, new byte[4])));

    [Fact]
    public void ToPng_ColorsUsedWhosePaletteOffsetWrapsInt_ReturnsNull() =>
        Assert.Null(DibFormat.ToPng(Dib(1, 1, 32, 0, new byte[4], colorsUsed: 1 << 29)));

    [Fact]
    public void ToPng_HeightOfIntMinValue_ReturnsNull() =>
        Assert.Null(DibFormat.ToPng(Dib(1, int.MinValue, 32, 0, new byte[4])));

    [Fact]
    public void FromBgra_WritesABottomUp32BitDib()
    {
        byte[] topDown = [1, 1, 1, 1, 2, 2, 2, 2];
        var dib = DibFormat.FromBgra(1, 2, topDown);

        Assert.Equal(40, BinaryPrimitives.ReadInt32LittleEndian(dib));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8)));
        Assert.Equal(32, BinaryPrimitives.ReadInt16LittleEndian(dib.AsSpan(14)));
        Assert.Equal(new byte[] { 2, 2, 2, 2, 1, 1, 1, 1 }, dib[40..]);
    }
}
