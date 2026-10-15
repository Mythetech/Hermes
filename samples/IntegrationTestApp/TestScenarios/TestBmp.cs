// Copyright (c) Mythetech. Licensed under the MIT License.
using System.Buffers.Binary;

namespace IntegrationTestApp.TestScenarios;

/// <summary>Builds a tiny uncompressed BMP so clipboard scenarios can offer an image that is not PNG.</summary>
internal static class TestBmp
{
    public static byte[] Create2x2()
    {
        const int width = 2;
        const int height = 2;
        const int headersLength = 14 + 40;
        const int stride = (width * 3 + 3) & ~3;
        const int pixelsLength = stride * height;

        var bmp = new byte[headersLength + pixelsLength];
        var span = bmp.AsSpan();

        span[0] = (byte)'B';
        span[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(span[2..], bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(span[10..], headersLength);

        BinaryPrimitives.WriteInt32LittleEndian(span[14..], 40);
        BinaryPrimitives.WriteInt32LittleEndian(span[18..], width);
        BinaryPrimitives.WriteInt32LittleEndian(span[22..], height);
        BinaryPrimitives.WriteInt16LittleEndian(span[26..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[28..], 24);
        BinaryPrimitives.WriteInt32LittleEndian(span[34..], pixelsLength);
        BinaryPrimitives.WriteInt32LittleEndian(span[38..], 2835);
        BinaryPrimitives.WriteInt32LittleEndian(span[42..], 2835);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = headersLength + y * stride + x * 3;
                span[pixel] = (byte)(x * 255);
                span[pixel + 1] = (byte)(y * 255);
                span[pixel + 2] = 0x80;
            }
        }

        return bmp;
    }
}
