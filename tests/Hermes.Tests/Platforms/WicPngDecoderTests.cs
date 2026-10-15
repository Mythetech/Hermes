// Copyright (c) Mythetech. Licensed under the MIT License.
#if WINDOWS
using System.Runtime.Versioning;
using Hermes.DataTransfer;
using Hermes.Platforms.Windows;
using Xunit;

namespace Hermes.Tests.Platforms;

[SupportedOSPlatform("windows")]
public sealed class WicPngDecoderTests
{
    [Fact]
    public void TryDecodeToBgra_ReturnsPixelsInBgraOrder()
    {
        byte[] rgba = [10, 20, 30, 255, 40, 50, 60, 128];
        var png = PngEncoder.EncodeRgba(2, 1, rgba);

        Assert.True(WicPngDecoder.TryDecodeToBgra(png, out var width, out var height, out var bgra));
        Assert.Equal(2, width);
        Assert.Equal(1, height);
        Assert.Equal(new byte[] { 30, 20, 10, 255, 60, 50, 40, 128 }, bgra);
    }

    [Fact]
    public void TryDecodeToBgra_Garbage_ReturnsFalse() =>
        Assert.False(WicPngDecoder.TryDecodeToBgra(new byte[] { 1, 2, 3 }, out _, out _, out _));
}
#endif
