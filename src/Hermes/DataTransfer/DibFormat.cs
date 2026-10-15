// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;

namespace Hermes.DataTransfer;

/// <summary>
/// Windows device-independent bitmaps (<c>CF_DIB</c>, <c>CF_DIBV5</c>): read as PNG, written from BGRA pixels.
/// </summary>
internal static class DibFormat
{
    private const int InfoHeaderSize = 40;
    private const int BiRgb = 0;
    private const int BiBitfields = 3;

    public static byte[]? ToPng(ReadOnlySpan<byte> dib)
    {
        if (dib.Length < InfoHeaderSize)
            return null;

        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(dib);
        var width = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
        var rawHeight = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
        var bitCount = BinaryPrimitives.ReadInt16LittleEndian(dib[14..]);
        var compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
        var colorsUsed = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);

        if (headerSize < InfoHeaderSize || headerSize > dib.Length || width <= 0 || rawHeight == 0)
            return null;
        if (bitCount is not (24 or 32))
            return null;
        if (compression != BiRgb && !(compression == BiBitfields && bitCount == 32))
            return null;

        // The masks follow a 40-byte header, or sit inside a V4/V5 header at the same offset.
        var masksSize = compression == BiBitfields && headerSize == InfoHeaderSize ? 12 : 0;
        if (compression == BiBitfields && !HasStandardMasks(dib))
            return null;

        var height = Math.Abs(rawHeight);
        var stride = (width * bitCount + 31) / 32 * 4;
        var pixelOffset = headerSize + masksSize + colorsUsed * 4;
        if (pixelOffset + (long)stride * height > dib.Length)
            return null;

        var bytesPerPixel = bitCount / 8;
        var rgba = new byte[width * height * 4];
        var anyAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var sourceRow = rawHeight > 0 ? height - 1 - y : y;
            var row = dib.Slice(pixelOffset + sourceRow * stride, stride);
            for (var x = 0; x < width; x++)
            {
                var source = row.Slice(x * bytesPerPixel, bytesPerPixel);
                var target = (y * width + x) * 4;
                rgba[target] = source[2];
                rgba[target + 1] = source[1];
                rgba[target + 2] = source[0];
                rgba[target + 3] = bitCount == 32 ? source[3] : (byte)255;
                anyAlpha |= rgba[target + 3] != 0;
            }
        }

        // 32-bit bitmaps from PrintScreen leave alpha at zero; showing them transparent would be wrong.
        if (bitCount == 32 && !anyAlpha)
        {
            for (var i = 3; i < rgba.Length; i += 4)
                rgba[i] = 255;
        }

        return PngEncoder.EncodeRgba(width, height, rgba);
    }

    public static byte[] FromBgra(int width, int height, ReadOnlySpan<byte> bgraTopDown)
    {
        var stride = width * 4;
        var dib = new byte[InfoHeaderSize + stride * height];
        BinaryPrimitives.WriteInt32LittleEndian(dib, InfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), width);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), height);
        BinaryPrimitives.WriteInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteInt16LittleEndian(dib.AsSpan(14), 32);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(16), BiRgb);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(20), stride * height);

        for (var y = 0; y < height; y++)
            bgraTopDown.Slice(y * stride, stride).CopyTo(dib.AsSpan(InfoHeaderSize + (height - 1 - y) * stride));

        return dib;
    }

    private static bool HasStandardMasks(ReadOnlySpan<byte> dib) =>
        dib.Length >= InfoHeaderSize + 12
        && BinaryPrimitives.ReadUInt32LittleEndian(dib[40..]) == 0x00FF0000
        && BinaryPrimitives.ReadUInt32LittleEndian(dib[44..]) == 0x0000FF00
        && BinaryPrimitives.ReadUInt32LittleEndian(dib[48..]) == 0x000000FF;
}
